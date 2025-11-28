using System.Globalization;
using System.Xml.Linq;

namespace PoctGateway.Core.Protocol.Poct1A;

public interface IPoctMessageFactory
{
    /// <summary>
    /// Creates an ACK.R01 document.
    /// </summary>
    XDocument CreateAck(
        string inboundControlId,
        string versionId,
        int outboundControlId,
        string? errorMessage);
}

public sealed class PoctMessageFactory : IPoctMessageFactory
{
    public XDocument CreateAck(
        string inboundControlId,
        string versionId,
        int outboundControlId,
        string? errorMessage)
    {
        var hasError = !string.IsNullOrWhiteSpace(errorMessage);
        var ackCode = hasError ? "AE" : "AA";

        var now = DateTimeOffset.Now;
        var creationDttm = now.ToString("yyyy-MM-dd'T'HH:mm:ss.ffK", CultureInfo.InvariantCulture);

        return new XDocument(
            new XElement("ACK.R01",
                new XElement("HDR",
                    new XElement("HDR.message_type", new XAttribute("V", "ACK.R01")),
                    new XElement("HDR.control_id",    new XAttribute("V", outboundControlId.ToString(CultureInfo.InvariantCulture))),
                    new XElement("HDR.version_id",    new XAttribute("V", versionId)),
                    new XElement("HDR.creation_dttm", new XAttribute("V", creationDttm))
                ),
                new XElement("ACK",
                    new XElement("ACK.type_cd",        new XAttribute("V", ackCode)),
                    new XElement("ACK.ack_control_id", new XAttribute("V", inboundControlId)),
                    hasError
                        ? new XElement("ACK.error_msg", new XAttribute("V", errorMessage!))
                        : null
                )
            )
        );
    }
}
