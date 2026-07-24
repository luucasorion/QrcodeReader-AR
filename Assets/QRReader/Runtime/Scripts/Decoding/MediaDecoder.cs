using System;
using System.Collections.Generic;
using QRReader.Classification;
using UnityEngine;

namespace QRReader.Decoding
{
    /// <summary>
    /// The decode stage of the pipeline (architecture.md §3.6, §4; M3-T5): given the classifier's
    /// <see cref="ContentKind"/> and the resolved bytes, it produces a <see cref="DecodedContent"/>
    /// handle or a typed <see cref="DecodeFailure"/> — the single value the lifecycle manager branches
    /// on (M5-T1). It routes <see cref="ContentKind.Image"/> to <see cref="ImageDecoder"/> and
    /// <see cref="ContentKind.Gif"/> to <see cref="GifDecoder"/>, and turns any failure into the shared
    /// error state rather than a crash (§8: "fail to the error state, never silently").
    /// </summary>
    /// <remarks>
    /// Pure and stateless like the two decoders it wraps, so it stays EditMode-unit-testable off device.
    /// The decoders already return <c>false</c> instead of throwing, but this stage still guards against
    /// an unexpected exception so a decode can <b>never</b> crash the app — the whole point of M3-T5 —
    /// and on any failure it leaks nothing (each decoder frees its own partial textures).
    /// </remarks>
    public static class MediaDecoder
    {
        /// <summary>
        /// Decodes <paramref name="bytes"/> according to <paramref name="kind"/>. Returns a
        /// <see cref="DecodeResult.Succeeded"/> handle for decodable image/GIF bytes;
        /// <see cref="DecodeFailure.Unsupported"/> when <paramref name="kind"/> is
        /// <see cref="ContentKind.Unsupported"/> (no decode attempted); and
        /// <see cref="DecodeFailure.Undecodable"/> for malformed/empty bytes. Never throws.
        /// </summary>
        public static DecodeResult Decode(ContentKind kind, byte[] bytes)
        {
            try
            {
                switch (kind)
                {
                    case ContentKind.Image:
                        return DecodeImage(bytes);
                    case ContentKind.Gif:
                        return DecodeGif(bytes);
                    default:
                        // Unsupported type: the documented website-support seam (§3.5). Route to error
                        // without touching the decoders.
                        return DecodeResult.Failed(DecodeFailure.Unsupported);
                }
            }
            catch (Exception ex)
            {
                // Defensive: the decoders shouldn't throw, but the M3-T5 contract is "never a crash".
                Debug.LogWarning($"{nameof(MediaDecoder)}: unexpected decode error: {ex.Message}");
                return DecodeResult.Failed(DecodeFailure.Undecodable);
            }
        }

        private static DecodeResult DecodeImage(byte[] bytes)
        {
            if (!ImageDecoder.TryDecode(bytes, out Texture2D texture))
            {
                return DecodeResult.Failed(DecodeFailure.Undecodable);
            }

            return DecodeResult.Succeeded(DecodedContent.ForImage(texture));
        }

        private static DecodeResult DecodeGif(byte[] bytes)
        {
            if (!GifDecoder.TryDecode(bytes, out IReadOnlyList<GifFrame> frames))
            {
                return DecodeResult.Failed(DecodeFailure.Undecodable);
            }

            return DecodeResult.Succeeded(DecodedContent.ForGif(frames));
        }
    }
}
