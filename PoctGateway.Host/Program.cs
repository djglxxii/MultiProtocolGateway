using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using Microsoft.Extensions.Logging;
using PoctGateway.Analyzers.GeneXpert;
using PoctGateway.Core.Engine;
using PoctGateway.Core.Session;
using PoctGateway.Core.Vendors;
using PoctGateway.Host.StubVendors;

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort) ? parsedPort : 9000;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Information)
        .AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
});

var logger = loggerFactory.CreateLogger("Gateway");

var vendorRegistry = BuildVendorRegistry(loggerFactory.CreateLogger("VendorRegistry"));

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
    logger.LogInformation("Shutdown requested. Stopping listener...");
};

await RunServerAsync(port, vendorRegistry, loggerFactory, logger, cts.Token);

return;

static VendorRegistry BuildVendorRegistry(ILogger logger)
{
    // Ensure at least these assemblies are loaded (optional but can help with lazy loading)
    _ = typeof(GeneXpertDevicePack);
    _ = typeof(Hl7StubDevicePack);
    _ = typeof(AstmStubDevicePack);
    _ = typeof(CustomBinaryDevicePack);

    // Make sure all referenced assemblies are loaded into the current AppDomain
    var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
    var entry = Assembly.GetEntryAssembly();
    if (entry != null)
    {
        var asm = typeof(GeneXpertDevicePack).Assembly;
        var asn = asm.GetName();
        var ass = entry.GetReferencedAssemblies().Where(a => a.FullName == asn.FullName);//.ToList();
        foreach (var name in entry.GetReferencedAssemblies())
        {
            if (assemblies.All(a => a.FullName != name.FullName))
            {
                assemblies.Add(Assembly.Load(name));
            }
        }
    }

    var packs = assemblies
        .SelectMany(SafeGetTypes)
        .Where(t =>
            typeof(IVendorDevicePack).IsAssignableFrom(t) &&
            !t.IsAbstract &&
            t.GetConstructor(Type.EmptyTypes) != null)
        .Select(t => (IVendorDevicePack)Activator.CreateInstance(t)!)
        .ToList();

    if (packs.Count == 0)
    {
        throw new InvalidOperationException("No IVendorDevicePack implementations were discovered.");
    }

    logger.LogInformation(
        "Loaded vendor packs: {Vendors}",
        string.Join(", ", packs.Select(p => $"{p.VendorKey} ({p.ProtocolKind})")));

    return new VendorRegistry(packs);
}

static IEnumerable<Type> SafeGetTypes(Assembly assembly)
{
    try
    {
        return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        return ex.Types.Where(t => t != null)!.Cast<Type>();
    }
}

static async Task RunServerAsync(int port, VendorRegistry vendorRegistry, ILoggerFactory loggerFactory, ILogger logger, CancellationToken cancellationToken)
{
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();
    logger.LogInformation("POCT gateway listening on port {Port}. Press Ctrl+C to exit.", port);

    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _ = Task.Run(() => HandleConnectionAsync(client, vendorRegistry, loggerFactory, cancellationToken), cancellationToken);
        }
    }
    finally
    {
        listener.Stop();
    }
}

static async Task HandleConnectionAsync(TcpClient client, VendorRegistry vendorRegistry, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
{
    using var tcp = client;
    var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
    var logger = loggerFactory.CreateLogger($"Session:{endpoint}");
    logger.LogInformation("Accepted connection from {Endpoint}.", endpoint);

    using var stream = client.GetStream();
    var ctx = new SessionContext(Guid.NewGuid(), endpoint, DateTimeOffset.UtcNow);

    var engine = new SessionEngine(
        ctx,
        vendorRegistry,
        sendRawAsync: async raw =>
        {
            var text = raw.EndsWith("\n", StringComparison.Ordinal) ? raw : raw + "\n";
            var bytes = Encoding.UTF8.GetBytes(text);
            await stream.WriteAsync(bytes, cancellationToken);
            ctx.MessageHistory.Add(new SessionMessage
            {
                MessageType = "OUTBOUND",
                Direction = MessageDirection.ServerToDevice,
                RawPayload = raw
            });
        },
        logInfo: message => logger.LogInformation("{Message}", message),
        logError: message => logger.LogError("{Message}", message));

    var buffer = new byte[8192];
   
    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            var payload = Encoding.UTF8.GetString(buffer, 0, read);

            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            try
            {
                await engine.ProcessInboundAsync(payload);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing inbound payload for session {SessionId}.", ctx.SessionId);
                return;
            }
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // Expected during shutdown.
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled exception for session {SessionId}.", ctx.SessionId);
    }
    finally
    {
        logger.LogInformation("Session {SessionId} closed.", ctx.SessionId);
    }
}

static string? ExtractLine(StringBuilder sb)
{
    for (var i = 0; i < sb.Length; i++)
    {
        if (sb[i] == '\n')
        {
            var line = sb.ToString(0, i).TrimEnd('\r');
            sb.Remove(0, i + 1);
            return line;
        }
    }

    return null;
}
