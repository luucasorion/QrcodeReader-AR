using System;
using System.Collections.Generic;
using UnityEngine;

namespace QRReader.Decoding
{
    /// <summary>
    /// Decodes animated (or single-frame) GIF bytes into per-frame textures plus their display delays
    /// using the mgGif library (architecture.md §3.6, ADR 0003; dependency added in M3-T1). Each
    /// frame's texture is <b>owned by the caller</b> for teardown (the M3-T4 handle); GIF frames are
    /// the primary memory risk on Quest (§7).
    /// </summary>
    /// <remarks>
    /// Pure and stateless like <see cref="ImageDecoder"/>, and it never throws: malformed bytes (or a
    /// GIF that yields no frames) return <c>false</c>, which the pipeline turns into the shared error
    /// state (decode-failure routing is M3-T5). Any textures created before a mid-stream failure are
    /// destroyed so a failed decode leaks nothing.
    /// </remarks>
    public static class GifDecoder
    {
        /// <summary>
        /// Attempts to decode <paramref name="bytes"/> into GIF frames. On success returns <c>true</c>
        /// and sets <paramref name="frames"/> to the decoded frames (in order); the caller owns every
        /// <see cref="GifFrame.Texture"/> and must destroy them. On failure returns <c>false</c> with
        /// a null list, leaking nothing.
        /// </summary>
        public static bool TryDecode(byte[] bytes, out IReadOnlyList<GifFrame> frames)
        {
            frames = null;

            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            var decoded = new List<GifFrame>();
            try
            {
                using var decoder = new MG.GIF.Decoder(bytes);

                MG.GIF.Image image;
                while ((image = decoder.NextImage()) != null)
                {
                    // CreateTexture() copies the current frame into a fresh, independent Texture2D, so
                    // each frame is safe to retain even though mgGif reuses its RawImage buffer between
                    // NextImage() calls (per-frame disposal composited into that shared buffer).
                    Texture2D texture = image.CreateTexture();
                    decoded.Add(new GifFrame(texture, image.Delay));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{nameof(GifDecoder)}: failed to decode GIF: {ex.Message}");
                DestroyFrames(decoded);
                return false;
            }

            if (decoded.Count == 0)
            {
                return false;
            }

            frames = decoded;
            return true;
        }

        // Free textures created before a mid-stream failure. Object.Destroy is the runtime path;
        // DestroyImmediate is required in edit mode (e.g. EditMode tests), where Destroy is disallowed.
        private static void DestroyFrames(List<GifFrame> frames)
        {
            foreach (GifFrame frame in frames)
            {
                if (frame.Texture == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(frame.Texture);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(frame.Texture);
                }
            }

            frames.Clear();
        }
    }
}
