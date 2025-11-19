using System.Xml.Linq;
using PoctGateway.Core.Protocol.Poct1A.DST;
using PoctGateway.Core.Protocol.Poct1A.EotR01;
using PoctGateway.Core.Protocol.Poct1A.HelR01;
using PoctGateway.Core.Protocol.Poct1A.ObsR01;
using PoctGateway.Core.Session;

namespace PoctGateway.Core.Helpers
{
    /// <summary>
    /// Extension helpers for working with POCT1A messages.
    /// </summary>
    public static class SessionContextExtensions
    {
        // Mapping of message type identifiers to factories that build the
        // corresponding POCO from an <see cref="XDocument"/>.  New message
        // types can be added here without modifying handler logic.
        private static readonly IReadOnlyDictionary<string, Func<XDocument, object>> _modelFactories
            = new Dictionary<string, Func<XDocument, object>>(StringComparer.OrdinalIgnoreCase)
        {
            { "HEL.R01", doc => new HelFacade(doc).ToModel() },
            { "DST.R01", doc => new DstFacade(doc).ToModel() },
            { "OBS.R01", doc => new ObsFacade(doc).ToModel() },
            { "EOT.R01", doc => new EotFacade(doc).ToModel() },
        };

        /// <summary>
        /// Returns a strongly typed model for the current session context based on the
        /// <see cref="SessionContext.MessageType"/>.  The type parameter must match
        /// the expected POCO returned by the underlying facade for the message type.
        /// </summary>
        /// <typeparam name="T">The expected POCO type.</typeparam>
        /// <param name="ctx">The session context supplying the message type and XML document.</param>
        /// <returns>An instance of <typeparamref name="T"/> built from <see cref="SessionContext.CurrentXDocument"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no current document is available.</exception>
        /// <exception cref="NotSupportedException">Thrown when the message type is not recognised.</exception>
        /// <exception cref="InvalidCastException">Thrown when the returned model cannot be cast to <typeparamref name="T"/>.</exception>
        public static T GetModel<T>(this SessionContext ctx) where T : class
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var doc = ctx.CurrentXDocument ?? throw new InvalidOperationException("CurrentXDocument is null.");

            if (!_modelFactories.TryGetValue(ctx.MessageType, out var factory))
            {
                throw new NotSupportedException($"Message type '{ctx.MessageType}' is not supported.");
            }

            var model = factory(doc);
            return model as T
                   ?? throw new InvalidCastException($"Unable to cast model of type '{model.GetType()}' to '{typeof(T)}'.");
        }

        /// <summary>
        /// Returns an untyped model for the current session context based on the
        /// <see cref="SessionContext.MessageType"/>.  Callers can examine the runtime
        /// type of the returned object or cast it to the appropriate POCO.
        /// </summary>
        /// <param name="ctx">The session context supplying the message type and XML document.</param>
        /// <returns>An object representing the deserialised message.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no current document is available.</exception>
        /// <exception cref="NotSupportedException">Thrown when the message type is not recognised.</exception>
        public static object GetModel(this SessionContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var doc = ctx.CurrentXDocument ?? throw new InvalidOperationException("CurrentXDocument is null.");

            if (!_modelFactories.TryGetValue(ctx.MessageType, out var factory))
            {
                throw new NotSupportedException($"Message type '{ctx.MessageType}' is not supported.");
            }

            return factory(doc);
        }
    }
}