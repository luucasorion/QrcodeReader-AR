using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// Builds the shared error-state icon procedurally (M4-T5) instead of shipping a PNG (PNGs are
    /// LFS-tracked): a filled warning disc with an exclamation glyph. The same icon is reused for every
    /// failure cause — resolve failure, timeout, unsupported type — per architecture.md §4/§8
    /// ("unsupported-type reuses the failure visual").
    /// </summary>
    /// <remarks>
    /// Parallels <see cref="SpinnerTextureFactory"/>: the returned <see cref="Texture2D"/> is
    /// <b>owned by the caller</b> and must be destroyed on teardown (§7); pixels are RGBA32 with a
    /// transparent surround so it composites over MR passthrough on the transparent quad material
    /// (M4-T1). Kept readable so it stays verifiable in EditMode tests.
    /// </remarks>
    public static class ErrorIconTextureFactory
    {
        private static readonly Color DefaultDisc = new Color(0.85f, 0.16f, 0.12f, 1f); // warning red
        private static readonly Color DefaultGlyph = Color.white;

        /// <summary>
        /// Creates a square error icon of <paramref name="size"/> px (clamped to ≥ 8): a
        /// <paramref name="discColor"/> disc with a <paramref name="glyphColor"/> exclamation mark.
        /// Caller owns and must destroy the texture.
        /// </summary>
        public static Texture2D Create(int size, Color? discColor = null, Color? glyphColor = null)
        {
            size = Mathf.Max(8, size);
            Color disc = discColor ?? DefaultDisc;
            Color glyph = glyphColor ?? DefaultGlyph;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "QrErrorIcon",
            };

            float center = (size - 1) * 0.5f;
            float discRadius = size * 0.5f - 1f;
            float edge = Mathf.Max(1f, size * 0.03f);

            // Exclamation mark geometry (texture space, y up): a rounded vertical bar in the upper
            // half and a dot below it.
            float barHalfWidth = size * 0.065f;
            float barTop = center + size * 0.24f;
            float barBottom = center - size * 0.02f;
            float dotCenterY = center - size * 0.18f;
            float dotRadius = size * 0.075f;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);

                    // Disc membership with a soft outer edge; transparent outside.
                    float discAlpha = 1f - SmoothStep01(discRadius, discRadius + edge, radius);

                    // Exclamation glyph coverage = bar ∪ dot.
                    float barCoverage = RoundedBarCoverage(x, y, center, barHalfWidth, barBottom, barTop, edge);
                    float dotDist = Mathf.Sqrt(dx * dx + (y - dotCenterY) * (y - dotCenterY));
                    float dotCoverage = 1f - SmoothStep01(dotRadius, dotRadius + edge, dotDist);
                    float glyphCoverage = Mathf.Max(barCoverage, dotCoverage) * discAlpha;

                    // Composite the glyph over the disc, both clipped to the disc.
                    Color pixel = Color.Lerp(disc, glyph, glyphCoverage);
                    pixel.a = disc.a * discAlpha;
                    pixels[y * size + x] = pixel;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        // Soft-edged coverage of a vertical bar spanning [bottom, top] in y, half-width in x.
        private static float RoundedBarCoverage(
            int x, int y, float centerX, float halfWidth, float bottom, float top, float edge)
        {
            float inX = 1f - SmoothStep01(halfWidth, halfWidth + edge, Mathf.Abs(x - centerX));
            float inY = SmoothStep01(bottom - edge, bottom, y) * (1f - SmoothStep01(top, top + edge, y));
            return inX * inY;
        }

        // GLSL-style smoothstep: 0 below e0, 1 above e1, smooth in between.
        private static float SmoothStep01(float e0, float e1, float x)
        {
            if (e1 <= e0)
            {
                return x >= e1 ? 1f : 0f;
            }

            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
