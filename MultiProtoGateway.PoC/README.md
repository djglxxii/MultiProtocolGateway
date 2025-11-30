# Multi-Protocol Gateway Proof of Concept

A proof-of-concept multi-protocol gateway demonstrating vendor detection and routing for POCT1A (XML) and HL7 protocols.

## Overview

This PoC demonstrates:

- **Single-port TCP listener** accepting connections from various device types
- **Automatic protocol detection** based on initial payload
- **Vendor routing** to appropriate session engines
- **Simple request/response** with protocol-appropriate ACK messages
- **Clean layered architecture** for production extensibility

## Solution Structure

```
MultiProtoGateway.PoC/
├── Gateway.Core/              # Protocol-agnostic core abstractions
│   ├── ProtocolKind.cs        # Protocol family enumeration
│   ├── DetectionResult.cs     # Vendor detection result
│   ├── SessionContext.cs      # Per-connection state
│   ├── HandlerBase.cs         # Pipeline handler base class
│   ├── HandlerPipeline.cs     # Handler execution pipeline
│   ├── IVendorDevicePack.cs   # Vendor pack interface
│   ├── ISessionEngine.cs      # Session engine interface
│   ├── ITransport.cs          # Transport abstraction
│   ├── TcpTransport.cs        # TCP transport implementation
│   ├── TextSessionEngine.cs   # Line-based session engine
│   └── VendorRegistry.cs      # Vendor pack registry
│
├── Gateway.Vendors.Sample/    # Sample vendor implementations
│   ├── Poct1ASampleVendorPack.cs   # POCT1A/XML vendor
│   ├── Hl7SampleVendorPack.cs      # HL7 vendor
│   └── Handlers/
│       ├── ConsoleLoggingHandler.cs  # Logging handler
│       ├── Poct1AEchoHandler.cs      # POCT1A ACK handler
│       └── Hl7EchoHandler.cs         # HL7 ACK handler
│
└── Gateway.Host/              # Console application
    └── Program.cs             # TCP listener and connection handling
```

## Requirements

- **.NET 9.0 SDK**
- No additional packages required

## Building

```bash
cd MultiProtoGateway.PoC
dotnet build
```

## Running

### Default port (5000)

```bash
dotnet run --project Gateway.Host
```

### Custom port

```bash
dotnet run --project Gateway.Host -- 5001
```

## Testing

### Using `nc` (netcat)

#### Test POCT1A/XML Protocol

```bash
# Connect and send XML message
nc localhost 5000
<HEL.R01><HDR><HDR.message_type V="HEL.R01"/></HDR></HEL.R01>
```

Expected response:
```xml
<ACK.R01><HDR><HDR.message_type V="ACK.R01"/></HDR><ACK><ACK.type_cd V="AA"/></ACK></ACK.R01>
```

You can continue sending XML messages and receive ACKs for each.

#### Test HL7 Protocol

```bash
# Connect and send HL7 message
nc localhost 5000
MSH|^~\&|DEVICE|LOC||GW||ORM^O01|1|P|2.3
```

Expected response:
```
MSH|^~\&|SERVER|GW||DEVICE||ACK|1|P|2.3
MSA|AA|1
```

### Using `telnet`

```bash
telnet localhost 5000
```

Then type your message and press Enter.

### Test script (multiple messages)

```bash
# POCT1A test
(echo '<HEL.R01><HDR><HDR.message_type V="HEL.R01"/></HDR></HEL.R01>'; sleep 1; echo '<PTL.Q01><HDR><HDR.message_type V="PTL.Q01"/></HDR></PTL.Q01>'; sleep 1) | nc localhost 5000

# HL7 test
(echo 'MSH|^~\&|DEVICE|LOC||GW||ORM^O01|1|P|2.3'; sleep 1; echo 'MSH|^~\&|DEVICE|LOC||GW||ADT^A01|2|P|2.3'; sleep 1) | nc localhost 5000
```

## Sample Output

Server console output when receiving connections:

```
========================================
  Multi-Protocol Gateway PoC
========================================
Registered vendor packs:
  - POCT1A-Sample (Poct1A)
  - HL7-Sample (Hl7)

Starting TCP listener on port 5000...
Press Ctrl+C to stop.

Listening on 0.0.0.0:5000

[CONNECT] Connection accepted from 127.0.0.1:54321
[DETECT] First line from 127.0.0.1:54321: <HEL.R01><HDR><HDR.message_type V="HEL.R01"/></HDR></HEL.R01>
[VENDOR] 127.0.0.1:54321 - Selected vendor: POCT1A-Sample (Protocol: Poct1A, Confidence: 100%)
[12:34:56.789] [POCT1A-Sample] [127.0.0.1:54321] Received: <HEL.R01><HDR><HDR.message_type V="HEL.R01"/></HDR></HEL.R01>
[12:34:56.790] [POCT1A] Sent ACK response
[DISCONNECT] 127.0.0.1:54321 - Connection closed
```

## Detection Logic

### POCT1A/XML Detection

- **100% confidence**: Message contains `<HEL.R01>` or `<DST.R01>`
- **90% confidence**: Message contains XML with `.R0` or `.Q0` suffixes (POCT1A message types)
- **50% confidence**: Message contains `<` and `>` (generic XML)

### HL7 Detection

- **100% confidence**: Message starts with `MSH|`
- Also detects MLLP-framed messages (starting with `0x0B`)

### Conflict Resolution

If multiple vendors match:
- Highest confidence wins
- Equal confidence = connection rejected (ambiguous)

## Important Notes

- **No TLS**: This PoC uses plain TCP connections
- **No Authentication**: No device authentication implemented
- **No Certificates**: No certificate handling
- **Line-based Protocol**: Messages are assumed to be single lines (terminated by newline)
- **Development Only**: This is a proof of concept, not production-ready

## Architecture Notes

The architecture is designed for easy extension:

1. **Adding New Protocols**: Implement `IVendorDevicePack` with detection logic and session creation
2. **Adding New Handlers**: Extend `HandlerBase` and add to vendor pack's handler list
3. **Custom Session Engines**: Implement `ISessionEngine` for non-text protocols
4. **Transport Flexibility**: `ITransport` abstraction allows swapping TCP for other transports

## License

Proof of concept - for demonstration purposes only.
