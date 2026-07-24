using System;

namespace QRReader.Decoding
{
    /// <summary>
    /// Why a <see cref="MediaDecoder.Decode"/> attempt did not produce a
    /// <see cref="DecodedContent"/> handle. Like <c>ResolveFailure</c> (M2-T5), every value routes to
    /// the renderer's single shared error visual (architecture.md §4, §8), but the cause stays distinct
    /// so the decode path can log honestly and future telemetry can tell the paths apart.
    /// </summary>
    public enum DecodeFailure
    {
        /// <summary>No failure — the result carries a handle. Only valid on a success result.</summary>
        None = 0,

        /// <summary>
        /// The classifier decided the bytes are a type we don't render (<see cref="Classification.ContentKind.Unsupported"/>);
        /// no decode was attempted. This is the documented seam for future website support (§3.5).
        /// </summary>
        Unsupported,

        /// <summary>
        /// The bytes were the expected kind but didn't decode — malformed/truncated image or GIF, or a
        /// GIF that yielded no frames. This is the malformed-input path M3-T5 must fail cleanly, never crash.
        /// </summary>
        Undecodable,
    }

    /// <summary>
    /// The typed outcome of decoding classified bytes (architecture.md §3.6, M3-T5): a
    /// <see cref="DecodedContent"/> handle that owns the decoded texture(s) on success, or a
    /// <see cref="DecodeFailure"/> reason otherwise. Mirrors the resolver's <c>ResolveResult</c> so the
    /// pipeline (M5-T1) can branch on a single value — success renders, any failure short-circuits to
    /// the shared error state — without inspecting decoder internals.
    /// </summary>
    public readonly struct DecodeResult
    {
        private DecodeResult(bool success, DecodedContent content, DecodeFailure failure)
        {
            Success = success;
            Content = content;
            Failure = failure;
        }

        /// <summary>True if decoding succeeded and <see cref="Content"/> holds the owned handle.</summary>
        public bool Success { get; }

        /// <summary>
        /// The decoded content handle on success; null on failure. The caller takes ownership and must
        /// <see cref="DecodedContent.Dispose"/> it on teardown to free the textures (§7, M5-T4).
        /// </summary>
        public DecodedContent Content { get; }

        /// <summary>The failure cause, or <see cref="DecodeFailure.None"/> when <see cref="Success"/>.</summary>
        public DecodeFailure Failure { get; }

        /// <summary>Builds a success result taking ownership of the decoded <paramref name="content"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
        public static DecodeResult Succeeded(DecodedContent content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            return new DecodeResult(true, content, DecodeFailure.None);
        }

        /// <summary>Builds a failure result for the given reason (no handle).</summary>
        /// <exception cref="ArgumentException"><paramref name="reason"/> is <see cref="DecodeFailure.None"/>.</exception>
        public static DecodeResult Failed(DecodeFailure reason)
        {
            if (reason == DecodeFailure.None)
            {
                throw new ArgumentException(
                    "A failure result requires a non-None reason.", nameof(reason));
            }

            return new DecodeResult(false, null, reason);
        }
    }
}
