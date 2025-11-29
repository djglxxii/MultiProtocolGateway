using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using PoctGateway.Core.Engine;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Protocol.Poct1A;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;
using Xunit;

namespace PoctGateway.Core.Tests;

public class SessionEngineOutboundQueueTests
{
    private readonly List<string> _sentMessages = new();
    private readonly List<string> _logMessages = new();
    
    private SessionEngine CreateEngine(IVendorDevicePack? vendor = null)
    {
        var context = new SessionContext(Guid.NewGuid(), "127.0.0.1:1234", DateTimeOffset.Now);
        var vendors = new[] { vendor ?? new TestVendorDevicePack() };
        var registry = new VendorRegistry(vendors);
        
        return new SessionEngine(
            context,
            registry,
            msg => { _sentMessages.Add(msg); return Task.CompletedTask; },
            msg => _logMessages.Add($"[INFO] {msg}"),
            msg => _logMessages.Add($"[ERROR] {msg}"),
            new PoctMessageFactory());
    }
    
    [Fact]
    public async Task OutboundQueue_DoesNotResetBetweenInboundMessages()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(QueueMultipleMessagesHandler));
        var engine = CreateEngine(vendor);
        
        // First inbound message - this will queue 2 outbound messages
        // Control IDs are assigned when messages are queued, so:
        // - First queued message gets control_id=1
        // - Second queued message gets control_id=2
        // - ACK gets control_id=3 (assigned after handler runs)
        var firstInbound = CreateTestHelMessage("1");
        await engine.ProcessInboundAsync(firstInbound);
        
        // Should have sent ACK + first queued message
        Assert.Equal(2, _sentMessages.Count); // ACK + first outbound
        
        _sentMessages.Clear();
        
        // Send ACK for the first outbound (control_id=1)
        var ackForFirst = CreateTestAck("1", "AA");
        await engine.ProcessInboundAsync(ackForFirst);
        
        // The second queued message should now be sent
        Assert.Single(_sentMessages);
    }

    [Fact]
    public async Task OutboundQueue_SendsOnlyOneMessageAtATime()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(QueueMultipleMessagesHandler));
        var engine = CreateEngine(vendor);
        
        // First inbound message - queues 2 messages
        var inbound = CreateTestHelMessage("1");
        await engine.ProcessInboundAsync(inbound);
        
        // Should send ACK first, then first outbound message
        // Second message should be held in queue
        Assert.Equal(2, _sentMessages.Count);
        
        // Verify first sent is ACK
        Assert.Contains("<ACK.R01>", _sentMessages[0]);
        
        // Verify second sent is the first queued message
        Assert.Contains("<TEST.MSG>", _sentMessages[1]);
    }

    [Fact]
    public async Task OutboundQueue_SendsNextMessageOnAck()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(QueueMultipleMessagesHandler));
        var engine = CreateEngine(vendor);
        
        // First inbound - queues 2 messages (control_id 1 and 2)
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        // Clear sent messages
        _sentMessages.Clear();
        
        // Send ACK for first outbound (control_id=1)
        await engine.ProcessInboundAsync(CreateTestAck("1", "AA"));
        
        // Second queued message should now be sent
        Assert.Single(_sentMessages);
        Assert.Contains("<TEST.MSG>", _sentMessages[0]);
    }

    [Fact]
    public async Task AckHandler_NotifiesListenerOnSuccess()
    {
        // Arrange
        var listener = new TestAckListener();
        var vendor = new TestVendorDevicePack(typeof(QueueWithListenerHandler));
        QueueWithListenerHandler.Listener = listener;
        
        var engine = CreateEngine(vendor);
        
        // First inbound - queues 1 message with listener (control_id=1)
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        // Send ACK for the outbound message (control_id=1)
        await engine.ProcessInboundAsync(CreateTestAck("1", "AA"));
        
        // Listener should have been notified
        Assert.Single(listener.AcknowledgedControlIds);
        Assert.Equal(1, listener.AcknowledgedControlIds[0]);
        Assert.Empty(listener.Errors);
    }

    [Fact]
    public async Task AckHandler_NotifiesListenerOnError()
    {
        // Arrange
        var listener = new TestAckListener();
        var vendor = new TestVendorDevicePack(typeof(QueueWithListenerHandler));
        QueueWithListenerHandler.Listener = listener;
        
        var engine = CreateEngine(vendor);
        
        // First inbound - queues 1 message with listener (control_id=1)
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        // Send NAK for the outbound message (control_id=1)
        await engine.ProcessInboundAsync(CreateTestAck("1", "AE", "Test error message"));
        
        // Listener should have been notified of error
        Assert.Empty(listener.AcknowledgedControlIds);
        Assert.Single(listener.Errors);
        Assert.Equal(1, listener.Errors[0].ControlId);
        Assert.Equal("Test error message", listener.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ProcessInboundAsync_DoesNotSendAckForAckMessages()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(NoOpHandler));
        var engine = CreateEngine(vendor);
        
        // Send initial message to establish vendor
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        _sentMessages.Clear();
        
        // Send an ACK message
        await engine.ProcessInboundAsync(CreateTestAck("99", "AA"));
        
        // Should not send any ACK response for ACK messages
        Assert.Empty(_sentMessages);
    }

    [Fact]
    public async Task AckHandler_IgnoresMismatchedControlId()
    {
        // Arrange
        var listener = new TestAckListener();
        var vendor = new TestVendorDevicePack(typeof(QueueWithListenerHandler));
        QueueWithListenerHandler.Listener = listener;
        
        var engine = CreateEngine(vendor);
        
        // First inbound - queues 1 message with listener, control_id will be 2
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        _sentMessages.Clear();
        
        // Send ACK with mismatched control ID
        await engine.ProcessInboundAsync(CreateTestAck("999", "AA")); // Wrong control ID
        
        // Listener should NOT have been notified
        Assert.Empty(listener.AcknowledgedControlIds);
        Assert.Empty(listener.Errors);
        
        // No messages should be sent (still waiting for correct ACK)
        Assert.Empty(_sentMessages);
    }
    
    private static string CreateTestHelMessage(string controlId)
    {
        return $@"<HEL.R01>
  <HDR>
    <HDR.message_type V=""HEL.R01"" />
    <HDR.control_id V=""{controlId}"" />
    <HDR.version_id V=""POCT1"" />
    <HDR.creation_dttm V=""2025-01-01T00:00:00Z"" />
  </HDR>
  <DCP>
    <DCP.device_id V=""TEST001"" />
    <DCP.vendor_id V=""TestVendor"" />
    <DCP.model_id V=""TestModel"" />
  </DCP>
</HEL.R01>";
    }
    
    private static string CreateTestAck(string ackControlId, string typeCd, string? errorMsg = null)
    {
        var errorElement = errorMsg != null 
            ? $@"<ACK.error_msg V=""{errorMsg}"" />" 
            : "";
        
        return $@"<ACK.R01>
  <HDR>
    <HDR.message_type V=""ACK.R01"" />
    <HDR.control_id V=""100"" />
    <HDR.version_id V=""POCT1"" />
    <HDR.creation_dttm V=""2025-01-01T00:00:00Z"" />
  </HDR>
  <ACK>
    <ACK.type_cd V=""{typeCd}"" />
    <ACK.ack_control_id V=""{ackControlId}"" />
    {errorElement}
  </ACK>
</ACK.R01>";
    }
}

public class TestVendorDevicePack : IVendorDevicePack
{
    private readonly Type[] _handlerTypes;
    
    public TestVendorDevicePack(params Type[] handlerTypes)
    {
        _handlerTypes = handlerTypes.Length > 0 ? handlerTypes : new[] { typeof(NoOpHandler) };
    }
    
    public string VendorKey => "TestVendor";
    public string ProtocolKind => "POCT1A";
    
    public bool IsMatch(RawInitialPacket packet)
    {
        return packet.RawText.Contains("TestVendor");
    }
    
    public IReadOnlyCollection<Type> GetHandlerTypes() => _handlerTypes;
}

public class NoOpHandler : HandlerBase
{
    public override Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        return next();
    }
}

public class QueueMultipleMessagesHandler : HandlerBase
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            // Queue two messages
            await SendAsync("<TEST.MSG><DATA>First</DATA></TEST.MSG>");
            await SendAsync("<TEST.MSG><DATA>Second</DATA></TEST.MSG>");
        }
        
        await next();
    }
}

public class QueueWithListenerHandler : HandlerBase, IOutboundAckListener
{
    public static IOutboundAckListener? Listener { get; set; }
    
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            await SendAsync("<TEST.MSG><DATA>WithListener</DATA></TEST.MSG>", Listener);
        }
        
        await next();
    }

    public void OnOutboundAcknowledged(int controlId) { }

    public bool OnOutboundError(int controlId, string? errorMessage)
    {
        return true;
    }
}

public class TestAckListener : IOutboundAckListener
{
    public List<int> AcknowledgedControlIds { get; } = new();
    public List<(int ControlId, string? ErrorMessage)> Errors { get; } = new();
    
    public void OnOutboundAcknowledged(int controlId)
    {
        AcknowledgedControlIds.Add(controlId);
    }

    public bool OnOutboundError(int controlId, string? errorMessage)
    {
        Errors.Add((controlId, errorMessage));
        return true;
    }
}
