using System;
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
        /// <see cref="UnityWebRequest"/>, applying the configured <see cref="ContentResolverConfig.TimeoutSeconds"/>.
        /// Returns the downloaded bytes on success, or <c>null</c> if the URL fails the HTTPS-only
        /// pre-check (<see cref="IsRequestAllowed"/>) or the request errors/times out.
        /// </summary>
        /// <remarks>
        /// Interim shape: the download-size cap (M2-T4) is enforced inside this method next, and the
        /// typed success/failure result carrying response headers (M2-T5) replaces the
        /// <c>byte[]</c>/<c>null</c> return. Must be awaited on Unity's main thread —
        /// <see cref="UnityWebRequest"/> is not thread-safe and its completion callback marshals back
        /// to the main thread.
        /// </remarks>
        public async Task<byte[]> GetAsync(string url)
        {
            // Double-guard: the pipeline should pre-check, but the resolver is the single guarded
            // network path, so never issue a request the transport gate would reject.
            if (!IsRequestAllowed(url))
            {
                return null;
            }

            using var request = UnityWebRequest.Get(url);
            request.timeout = _config.TimeoutSeconds;

            await AwaitRequest(request.SendWebRequest());

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"{nameof(ContentResolver)}: GET failed for '{url}': {request.result} ({request.error}).");
                return null;
            }

            return request.downloadHandler.data;
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
    }
}
