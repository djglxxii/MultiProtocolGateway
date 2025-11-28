using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;
using PoctGateway.Core.Engine;
using PoctGateway.Core.Handlers;
using PoctGateway.Core.Protocol.Poct1A;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;
using Xunit;

namespace PoctGateway.Core.Tests;

public class SessionEngineTokenReplacementTests
{
    private readonly List<string> _sentMessages = new();
    private readonly List<string> _logMessages = new();
    
    private SessionEngine CreateEngine(IVendorDevicePack vendor)
    {
        var context = new SessionContext(Guid.NewGuid(), "127.0.0.1:1234", DateTimeOffset.Now);
        var registry = new VendorRegistry(new[] { vendor });
        
        return new SessionEngine(
            context,
            registry,
            msg => { _sentMessages.Add(msg); return Task.CompletedTask; },
            msg => _logMessages.Add($"[INFO] {msg}"),
            msg => _logMessages.Add($"[ERROR] {msg}"),
            new PoctMessageFactory());
    }
    
    [Fact]
    public async Task TokenReplacement_ReplacesControlIdToken()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        TokenMessageHandler.MessageTemplate = @"<MSG><HDR.control_id V=""{{ control_id }}"" /></MSG>";
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        // Find the message that's not the ACK
        var msgPayload = _sentMessages.Find(m => m.Contains("<MSG>"));
        Assert.NotNull(msgPayload);
        
        // The control_id token should be replaced with an actual number
        // Control IDs are assigned when messages are queued, so handler message gets control_id=1
        Assert.DoesNotContain("{{ control_id }}", msgPayload);
        Assert.Contains(@"V=""1""", msgPayload);
    }

    [Fact]
    public async Task TokenReplacement_ReplacesDateTimeNowToken()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        TokenMessageHandler.MessageTemplate = @"<MSG><HDR.creation_dttm V=""{{ datetime_now }}"" /></MSG>";
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        var msgPayload = _sentMessages.Find(m => m.Contains("<MSG>"));
        Assert.NotNull(msgPayload);
        
        // The datetime_now token should be replaced
        Assert.DoesNotContain("{{ datetime_now }}", msgPayload);
        // Should contain a valid ISO datetime format
        Assert.Contains("20", msgPayload); // Year starts with 20xx
        Assert.Contains("T", msgPayload); // ISO format has 'T' separator
    }

    [Fact]
    public async Task TokenReplacement_ReplacesDateTimeNowWithCustomFormat()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        TokenMessageHandler.MessageTemplate = @"<MSG><HDR.creation_dttm V=""{{ datetime_now:yyyy-MM-dd }}"" /></MSG>";
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        var msgPayload = _sentMessages.Find(m => m.Contains("<MSG>"));
        Assert.NotNull(msgPayload);
        
        // The datetime_now token should be replaced with the custom format
        Assert.DoesNotContain("{{ datetime_now:", msgPayload);
        // Should contain date in yyyy-MM-dd format (no 'T' since we didn't include time)
        var today = DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Contains(today, msgPayload);
    }

    [Fact]
    public async Task TokenReplacement_IsCaseInsensitive()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        TokenMessageHandler.MessageTemplate = @"<MSG><HDR.control_id V=""{{ CONTROL_ID }}"" /></MSG>";
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        var msgPayload = _sentMessages.Find(m => m.Contains("<MSG>"));
        Assert.NotNull(msgPayload);
        
        // Upper case token should also be replaced (control_id=1 is assigned first)
        Assert.DoesNotContain("{{ CONTROL_ID }}", msgPayload);
        Assert.Contains(@"V=""1""", msgPayload);
    }

    [Fact]
    public async Task HdrInjection_InjectsHdrWhenMissing()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        // Message without HDR element
        TokenMessageHandler.MessageTemplate = @"<CUSTOM.MSG><DATA V=""test"" /></CUSTOM.MSG>";
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        var msgPayload = _sentMessages.Find(m => m.Contains("<CUSTOM.MSG>"));
        Assert.NotNull(msgPayload);
        
        // HDR should be injected
        Assert.Contains("<HDR>", msgPayload);
        Assert.Contains("<HDR.message_type", msgPayload);
        Assert.Contains(@"V=""CUSTOM.MSG""", msgPayload); // message_type should be root name
        Assert.Contains("<HDR.control_id", msgPayload);
        Assert.Contains("<HDR.version_id", msgPayload);
        Assert.Contains(@"V=""POCT1""", msgPayload);
        Assert.Contains("<HDR.creation_dttm", msgPayload);
    }

    [Fact]
    public async Task HdrInjection_DoesNotDuplicateExistingHdr()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        // Message with existing HDR element
        TokenMessageHandler.MessageTemplate = @"<CUSTOM.MSG><HDR><HDR.control_id V=""999"" /></HDR><DATA V=""test"" /></CUSTOM.MSG>";
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        var msgPayload = _sentMessages.Find(m => m.Contains("<CUSTOM.MSG>"));
        Assert.NotNull(msgPayload);
        
        // Should have only one HDR
        var hdrCount = System.Text.RegularExpressions.Regex.Matches(msgPayload, "<HDR>").Count;
        Assert.Equal(1, hdrCount);
        
        // Original control_id should be preserved (no tokens)
        Assert.Contains(@"V=""999""", msgPayload);
    }

    [Fact]
    public async Task ControlId_IncrementsForEachMessage()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(MultiTokenMessageHandler));
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        // Only 1 message is sent initially (first message waits for ACK before second is sent)
        // But both messages are queued with sequential control IDs
        // Find first message sent
        var firstMessage = _sentMessages.Find(m => m.Contains("<MULTI.MSG>"));
        Assert.NotNull(firstMessage);
        
        var doc1 = XDocument.Parse(firstMessage);
        var controlId1 = int.Parse(doc1.Root!.Element("HDR")!.Element("HDR.control_id")!.Attribute("V")!.Value);
        
        // First queued message should have control_id=1
        Assert.Equal(1, controlId1);
        
        // Now send ACK for first message to trigger sending of second
        _sentMessages.Clear();
        await engine.ProcessInboundAsync(CreateTestAck("1", "AA"));
        
        var secondMessage = _sentMessages.Find(m => m.Contains("<MULTI.MSG>"));
        Assert.NotNull(secondMessage);
        
        var doc2 = XDocument.Parse(secondMessage);
        var controlId2 = int.Parse(doc2.Root!.Element("HDR")!.Element("HDR.control_id")!.Attribute("V")!.Value);
        
        // Second message should have control_id=2
        Assert.Equal(2, controlId2);
        Assert.True(controlId2 > controlId1);
    }
    
    private static string CreateTestAck(string ackControlId, string typeCd)
    {
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
  </ACK>
</ACK.R01>";
    }

    [Fact]
    public async Task ControlId_StartsAt1ForSession()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        TokenMessageHandler.MessageTemplate = @"<MSG><HDR.control_id V=""{{ control_id }}"" /></MSG>";
        
        // Create a fresh engine
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        // Control IDs are assigned when messages are queued
        // First queued message gets control_id=1
        var msgPayload = _sentMessages.Find(m => m.Contains("<MSG>"));
        Assert.NotNull(msgPayload);
        Assert.Contains(@"V=""1""", msgPayload);
    }

    [Fact]
    public async Task NonXmlPayload_IsPassedThrough()
    {
        // Arrange
        var vendor = new TestVendorDevicePack(typeof(TokenMessageHandler));
        TokenMessageHandler.MessageTemplate = "PLAIN TEXT MESSAGE";
        
        var engine = CreateEngine(vendor);
        await engine.ProcessInboundAsync(CreateTestHelMessage("1"));
        
        var msgPayload = _sentMessages.Find(m => m.Contains("PLAIN TEXT MESSAGE"));
        Assert.NotNull(msgPayload);
        Assert.Equal("PLAIN TEXT MESSAGE", msgPayload);
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
}

public class TokenMessageHandler : HandlerBase
{
    public static string MessageTemplate { get; set; } = "";
    
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            await SendAsync(MessageTemplate);
        }
        
        await next();
    }
}

public class MultiTokenMessageHandler : HandlerBase
{
    public override async Task HandleAsync(SessionContext ctx, Func<Task> next)
    {
        if (ctx.MessageType == "HEL.R01")
        {
            // Queue two messages without HDR (they will get HDR injected)
            await SendAsync("<MULTI.MSG><DATA>First</DATA></MULTI.MSG>");
            await SendAsync("<MULTI.MSG><DATA>Second</DATA></MULTI.MSG>");
        }
        
        await next();
    }
}
