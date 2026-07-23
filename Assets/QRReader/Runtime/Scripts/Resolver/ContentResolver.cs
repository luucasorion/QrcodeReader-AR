using System;
using System.IO;
using System.Threading.Tasks;
using QRReader.Configuration;
using UnityEngine;
using UnityEngine.Networking;

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

        /// <summary>
        /// Performs the <c>https</c> GET (M2-T3) for an allowed payload URL via
        /// <see cref="UnityWebRequest"/>, applying the configured <see cref="ContentResolverConfig.TimeoutSeconds"/>,
        /// and returns the typed <see cref="ResolveResult"/> (M2-T5): downloaded bytes + response
        /// headers on success, or a <see cref="ResolveFailure"/> reason on any guard violation or
        /// transport failure. This is the single guarded network path (§8), so it <em>never throws
        /// into the pipeline</em> — every failure is a value, not an exception.
        /// </summary>
        /// <remarks>
        /// The download-size cap (M2-T4) is enforced <em>while streaming</em> by
        /// <see cref="CappedDownloadHandler"/>: an oversized <c>Content-Length</c> is rejected up
        /// front and the transfer is aborted the moment received bytes exceed
        /// <see cref="ContentResolverConfig.MaxDownloadBytes"/>, so an untrusted server can't exhaust
        /// device memory (architecture.md §7). Must be awaited on Unity's main thread —
        /// <see cref="UnityWebRequest"/> is not thread-safe and its completion callback marshals back
        /// to the main thread.
        /// </remarks>
        public async Task<ResolveResult> GetAsync(string url)
        {
            // Double-guard: the pipeline should pre-check, but the resolver is the single guarded
            // network path, so never issue a request the transport gate would reject.
            if (!IsRequestAllowed(url))
            {
                return ResolveResult.Failed(ResolveFailure.BlockedByPolicy);
            }

            try
            {
                var cappedHandler = new CappedDownloadHandler(_config.MaxDownloadBytes);
                using var request = UnityWebRequest.Get(url);
                request.downloadHandler = cappedHandler;
                request.timeout = _config.TimeoutSeconds;

                await AwaitRequest(request.SendWebRequest());

                // Check the cap first: aborting the transfer surfaces as a request error, but the real
                // cause is the oversized body, which we want to report distinctly (and honestly).
                if (cappedHandler.ExceededCap)
                {
                    Debug.LogWarning(
                        $"{nameof(ContentResolver)}: download for '{url}' exceeded the " +
                        $"{_config.MaxDownloadMebibytes} MiB cap; aborted.");
                    return ResolveResult.Failed(ResolveFailure.ExceededSizeCap);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ResolveFailure reason = ClassifyRequestFailure(request);
                    Debug.LogWarning(
                        $"{nameof(ContentResolver)}: GET failed for '{url}': {reason} " +
                        $"({request.result}: {request.error}).");
                    return ResolveResult.Failed(reason);
                }

                return ResolveResult.Succeeded(cappedHandler.data, request.GetResponseHeaders());
            }
            catch (Exception ex)
            {
                // The resolver must never throw into the pipeline: an unexpected transport error still
                // surfaces as a failure result so the caller routes to the error state (§4).
                Debug.LogWarning($"{nameof(ContentResolver)}: GET for '{url}' threw: {ex.Message}");
                return ResolveResult.Failed(ResolveFailure.NetworkError);
            }
        }

        // A timed-out UnityWebRequest surfaces as a ConnectionError whose message names the timeout;
        // there is no dedicated Result value, so match on that to keep the cause distinct (best effort
        // — anything else is a generic network error). Both route to the same error visual regardless.
        private static ResolveFailure ClassifyRequestFailure(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError
                && !string.IsNullOrEmpty(request.error)
                && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveFailure.Timeout;
            }

            return ResolveFailure.NetworkError;
        }

        // Bridges UnityWebRequest's async operation to await without needing a coroutine host, so the
        // resolver stays a plain class. The completed callback fires on the main thread.
        private static Task AwaitRequest(UnityWebRequestAsyncOperation operation)
        {
            if (operation.isDone)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }

        /// <summary>
        /// Streaming download handler that enforces the size cap (M2-T4) as bytes arrive, rather than
        /// buffering the whole (untrusted) response first. Rejects an oversized declared
        /// <c>Content-Length</c> up front and aborts the transfer the moment accumulated bytes would
        /// exceed the cap. Guards device memory against a hostile/oversized payload (§7).
        /// </summary>
        private sealed class CappedDownloadHandler : DownloadHandlerScript
        {
            private readonly long _maxBytes;
            private readonly MemoryStream _received = new();

            /// <summary>True once the declared or streamed size exceeded the cap (transfer aborted).</summary>
            public bool ExceededCap { get; private set; }

            // 64 KiB preallocated read buffer, reused for every chunk (avoids per-chunk allocation).
            public CappedDownloadHandler(long maxBytes) : base(new byte[64 * 1024])
            {
                _maxBytes = maxBytes;
            }

            // Fast-reject: if the server declares an oversized body, don't download it at all.
            protected override void ReceiveContentLengthHeader(ulong contentLength)
            {
                if (contentLength > (ulong)_maxBytes)
                {
                    ExceededCap = true;
                }
            }

            // Returning false aborts the in-flight request. Enforce the cap on the running total.
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (ExceededCap || data == null || dataLength <= 0)
                {
                    return !ExceededCap;
                }

                if (_received.Length + dataLength > _maxBytes)
                {
                    ExceededCap = true;
                    return false;
                }

                _received.Write(data, 0, dataLength);
                return true;
            }

            protected override byte[] GetData() => _received.ToArray();
        }
    }
}
