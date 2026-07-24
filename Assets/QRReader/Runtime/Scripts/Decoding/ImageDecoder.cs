using UnityEngine;

namespace QRReader.Decoding
{
    /// <summary>
    /// Decodes downloaded still-image bytes (PNG/JPEG) into a Unity <see cref="Texture2D"/>
    /// (architecture.md §3.6). The decoded texture is <b>owned by the caller</b>: it must be destroyed
    /// on teardown so it doesn't leak GPU memory on Quest (§7). The M3-T4 decoded-content handle takes
    /// that ownership; M5-T4 frees it on <c>TrackableRemoved</c>.
    /// </summary>
    /// <remarks>
    /// Pure and stateless (like <c>ContentTypeClassifier</c>) so it stays unit-testable off device.
    /// It never throws: malformed bytes return <c>false</c>, which the pipeline turns into the shared
    /// error state (the decode-failure routing is M3-T5). GIFs are handled separately by the mgGif
    /// path (M3-T3) — this decoder is only for single-frame images.
    /// </remarks>
    public static class ImageDecoder
    {
        /// <summary>
        /// Attempts to decode <paramref name="bytes"/> into a texture. On success, returns <c>true</c>
        /// and sets <paramref name="texture"/> to a newly created <see cref="Texture2D"/> the caller
        /// owns and must destroy. On failure (null/empty or undecodable bytes) returns <c>false</c>
        /// with a null texture, leaking nothing.
        /// </summary>
        public static bool TryDecode(byte[] bytes, out Texture2D texture)
        {
            texture = null;

            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            // 2x2 placeholder; LoadImage replaces it with the real image dimensions. markNonReadable
            // frees the CPU-side pixel copy after the GPU upload — we only display, never read back —
            // which halves the per-texture footprint on Quest (§7).
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!decoded.LoadImage(bytes, markNonReadable: true))
            {
                // Undecodable: destroy the throwaway texture so a failed decode leaks nothing.
                DestroyTexture(decoded);
                return false;
            }

            texture = decoded;
            return true;
        }

        // Object.Destroy is the runtime path; DestroyImmediate is required in edit mode (e.g. EditMode
        // tests), where Destroy is disallowed. Keeps the decoder usable and leak-free in both.
        private static void DestroyTexture(Object obj)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
