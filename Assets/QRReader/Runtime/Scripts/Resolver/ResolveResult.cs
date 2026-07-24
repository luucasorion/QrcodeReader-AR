using System;
using System.Collections.Generic;

namespace QRReader.Resolver
{
    /// <summary>
    /// Why a <see cref="ContentResolver.GetAsync"/> attempt did not produce bytes. Every value
    /// routes to the renderer's single shared error visual (architecture.md §4), but the cause is
    /// kept distinct so the resolver can log honestly and future telemetry can tell the paths apart.
    /// </summary>
    public enum ResolveFailure
    {
        /// <summary>No failure — the result carries bytes. Only valid on a success result.</summary>
        None = 0,

        /// <summary>The URL failed the HTTPS-only transport gate; no request was made (§8, ADR 0003).</summary>
        BlockedByPolicy,

        /// <summary>The request exceeded the configured timeout before completing (§3.4).</summary>
        Timeout,

        /// <summary>The declared or streamed body exceeded the configured size cap (§7, M2-T4).</summary>
        ExceededSizeCap,

        /// <summary>Any other transport/protocol failure (connection, HTTP status, malformed data).</summary>
        NetworkError,
    }

    /// <summary>
    /// The typed outcome of resolving a QR payload URL (architecture.md §3.4, M2-T5): the downloaded
    /// bytes plus response headers on success, or a <see cref="ResolveFailure"/> reason otherwise.
    /// Replaces the interim <c>byte[]</c>/<c>null</c> return so the untrusted-input path surfaces a
    /// single value the pipeline can branch on without inspecting <see cref="ContentResolver"/> state,
    /// and so the classifier (M2-T6) can read the <c>Content-Type</c> header from the same result.
    /// </summary>
    public readonly struct ResolveResult
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
            new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);

        private ResolveResult(
            bool success,
            byte[] bytes,
            IReadOnlyDictionary<string, string> headers,
            ResolveFailure failure)
        {
            Success = success;
            Bytes = bytes;
            Headers = headers;
            Failure = failure;
        }

        /// <summary>True if the download succeeded and <see cref="Bytes"/> holds the content.</summary>
        public bool Success { get; }

        /// <summary>The downloaded bytes on success; empty (never null) on failure.</summary>
        public byte[] Bytes { get; }

        /// <summary>
        /// Response headers on success (case-insensitive keys); empty on failure. Never null. Read via
        /// <see cref="TryGetContentType"/> for the classifier's <c>Content-Type</c> fallback (§3.5).
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>The failure cause, or <see cref="ResolveFailure.None"/> when <see cref="Success"/>.</summary>
        public ResolveFailure Failure { get; }

        /// <summary>Builds a success result carrying the downloaded bytes and response headers.</summary>
        /// <remarks>
        /// Null <paramref name="bytes"/>/<paramref name="headers"/> are normalized to empty so callers
        /// never have to null-check a successful result. Header keys are made case-insensitive.
        /// </remarks>
        public static ResolveResult Succeeded(byte[] bytes, IReadOnlyDictionary<string, string> headers)
        {
            var normalized = headers == null ? EmptyHeaders : CopyCaseInsensitive(headers);
            return new ResolveResult(true, bytes ?? Array.Empty<byte>(), normalized, ResolveFailure.None);
        }

        /// <summary>Builds a failure result for the given reason (no bytes, no headers).</summary>
        /// <exception cref="ArgumentException"><paramref name="reason"/> is <see cref="ResolveFailure.None"/>.</exception>
        public static ResolveResult Failed(ResolveFailure reason)
        {
            if (reason == ResolveFailure.None)
            {
                throw new ArgumentException(
                    "A failure result requires a non-None reason.", nameof(reason));
            }

            return new ResolveResult(false, Array.Empty<byte>(), EmptyHeaders, reason);
        }

        /// <summary>
        /// Reads the <c>Content-Type</c> response header (case-insensitive), the classifier's fallback
        /// after the URL extension hint (§3.5). Returns false when absent — including on any failure.
        /// </summary>
        public bool TryGetContentType(out string contentType) =>
            Headers.TryGetValue("Content-Type", out contentType) && !string.IsNullOrEmpty(contentType);

        // GetResponseHeaders() already returns a Dictionary, but copy into a case-insensitive one so
        // header lookup (Content-Type) is robust and the result doesn't alias the transport's own map.
        private static IReadOnlyDictionary<string, string> CopyCaseInsensitive(
            IReadOnlyDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(source.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
