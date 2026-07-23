namespace QRReader.Resolver
{
    /// <summary>
    /// Running accountant for the download-size cap (M2-T4, architecture.md §7). Extracted from the
    /// streaming download handler so the memory guard — the property that an untrusted server can't
    /// push more than the configured bytes into device memory — is unit-testable off device (M2-T7)
    /// without a live transfer.
    /// </summary>
    /// <remarks>
    /// Enforces the cap two ways: a declared <c>Content-Length</c> that already exceeds the cap is
    /// rejected up front, and the accumulated streamed byte count is rejected the moment it would
    /// cross the cap. Once <see cref="Exceeded"/> trips it stays tripped. A total exactly equal to the
    /// cap is allowed; only strictly greater is over.
    /// </remarks>
    internal sealed class DownloadSizeGuard
    {
        private readonly long _maxBytes;
        private long _received;

        public DownloadSizeGuard(long maxBytes)
        {
            _maxBytes = maxBytes;
        }

        /// <summary>True once the declared or streamed size exceeded the cap.</summary>
        public bool Exceeded { get; private set; }

        /// <summary>
        /// Records a server-declared <c>Content-Length</c>. Trips <see cref="Exceeded"/> and returns
        /// <c>false</c> if it already exceeds the cap, so the caller can reject before downloading.
        /// </summary>
        public bool DeclareContentLength(ulong contentLength)
        {
            if (contentLength > (ulong)_maxBytes)
            {
                Exceeded = true;
            }

            return !Exceeded;
        }

        /// <summary>
        /// Accounts for a streamed chunk of <paramref name="chunkLength"/> bytes. Trips
        /// <see cref="Exceeded"/> and returns <c>false</c> if the running total would cross the cap;
        /// a non-positive length is a no-op that neither advances the total nor trips the guard.
        /// </summary>
        public bool Account(int chunkLength)
        {
            if (Exceeded || chunkLength <= 0)
            {
                return !Exceeded;
            }

            if (_received + chunkLength > _maxBytes)
            {
                Exceeded = true;
                return false;
            }

            _received += chunkLength;
            return true;
        }
    }
}
