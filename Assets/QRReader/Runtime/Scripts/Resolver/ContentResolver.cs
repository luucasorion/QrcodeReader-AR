using System;
using QRReader.Configuration;

namespace QRReader.Resolver
{
    /// <summary>
    /// Downloads the content referenced by a QR payload URL under the safety guards in
    /// <see cref="ContentResolverConfig"/> (architecture.md §3.4, ADR 0003). The payload is untrusted
    /// input, so this is the only place that performs <c>https</c> GETs from QR payloads (§8).
    /// </summary>
    /// <remarks>
    /// Built incrementally across M2: this task (M2-T1's config → M2-T2) adds the HTTPS-only
    /// pre-check that gates any request. The GET + timeout (M2-T3), download-size cap (M2-T4), and
    /// the typed success/failure result (M2-T5) build on this gate. Kept as a plain class (no
    /// <c>MonoBehaviour</c>/MRUK dependency) so it stays EditMode-unit-testable off device
    /// (plan R1, §8); the lifecycle manager constructs it with a serialized config.
    /// </remarks>
    public sealed class ContentResolver
    {
        private readonly ContentResolverConfig _config;

        /// <summary>Creates a resolver bound to the given guard configuration.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public ContentResolver(ContentResolverConfig config)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// HTTPS-only pre-check (M2-T2): returns <c>true</c> only if the payload URL may be requested.
        /// When <see cref="ContentResolverConfig.HttpsOnly"/> is enabled, a payload is allowed only if
        /// it is a well-formed absolute <c>https://</c> URI; <c>http://</c>, other schemes, and
        /// relative/malformed URLs are rejected before any network request is made. A null/empty/
        /// whitespace payload is never requestable and is always rejected.
        /// </summary>
        /// <remarks>
        /// This is the transport gate only. Reachability, timeout, size cap, and the typed result are
        /// handled by later tasks (M2-T3–T5); this method deliberately performs no network I/O.
        /// </remarks>
        public bool IsRequestAllowed(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!_config.HttpsOnly)
            {
                return true;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                   && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }
    }
}
