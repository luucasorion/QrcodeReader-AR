using UnityEngine;

namespace QRReader.Configuration
{
    /// <summary>
    /// Serialized configuration for the content resolver's safety guards (architecture.md §3.4, §3.8;
    /// ADR 0003). The QR payload URL is untrusted input, so these guards are mandatory and — per the
    /// project's "configurable values stay configurable" convention (§8) — authored here rather than
    /// hard-coded.
    /// </summary>
    /// <remarks>
    /// This is a stub introduced in M2 (implementation-plan.md M2-T1) and completed in M6. It covers
    /// the resolver guards only; the renderer's scale factor is added to configuration separately in
    /// M4-T2. Create an asset via <c>Assets ▸ Create ▸ QR Reader ▸ Content Resolver Config</c> and
    /// hand it to the resolver.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ContentResolverConfig",
        menuName = "QR Reader/Content Resolver Config",
        order = 0)]
    public sealed class ContentResolverConfig : ScriptableObject
    {
        /// <summary>Default request timeout, in seconds (project context §3.4).</summary>
        public const int DefaultTimeoutSeconds = 10;

        /// <summary>Default download cap, in mebibytes (project context §3.4, "~25 MB").</summary>
        public const int DefaultMaxDownloadMebibytes = 25;

        private const long BytesPerMebibyte = 1024L * 1024L;

        [SerializeField]
        [Tooltip("Reject any payload URL that is not https:// before making a request. Keep enabled; " +
                 "disabling it removes the untrusted-input transport guard.")]
        private bool _httpsOnly = true;

        [SerializeField]
        [Min(1)]
        [Tooltip("Abort the download if it exceeds this many seconds.")]
        private int _timeoutSeconds = DefaultTimeoutSeconds;

        [SerializeField]
        [Min(1)]
        [Tooltip("Fail the download if the content exceeds this size (MiB), whether reported by " +
                 "Content-Length or observed in the streamed bytes.")]
        private int _maxDownloadMebibytes = DefaultMaxDownloadMebibytes;

        /// <summary>When true, the resolver rejects non-<c>https://</c> URLs before any request.</summary>
        public bool HttpsOnly => _httpsOnly;

        /// <summary>Request timeout in seconds (always ≥ 1).</summary>
        public int TimeoutSeconds => _timeoutSeconds;

        /// <summary>Download size cap in mebibytes (always ≥ 1).</summary>
        public int MaxDownloadMebibytes => _maxDownloadMebibytes;

        /// <summary>Download size cap in bytes, derived from <see cref="MaxDownloadMebibytes"/>.</summary>
        public long MaxDownloadBytes => _maxDownloadMebibytes * BytesPerMebibyte;

        // Clamp to sane minimums so an asset edited to 0/negative can't disable a guard by accident.
        private void OnValidate()
        {
            if (_timeoutSeconds < 1)
            {
                _timeoutSeconds = 1;
            }

            if (_maxDownloadMebibytes < 1)
            {
                _maxDownloadMebibytes = 1;
            }
        }
    }
}
