using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// Builds the loading-spinner sprite procedurally (M4-T4) instead of shipping a PNG: a ring whose
    /// alpha fades around the circumference (a comet "tail"), which reads as a spinner once rotated by
    /// <see cref="LoadingSpinner"/>. Generating it in code keeps the visual self-contained (no LFS
    /// binary asset) and lets the colour follow the app.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="Texture2D"/> is <b>owned by the caller</b> and must be destroyed on
    /// teardown (§7), the same ownership rule as the decoded content textures. Pixels are RGBA32 with a
    /// transparent centre so it composites cleanly over MR passthrough on the transparent quad material
    /// (M4-T1). Kept readable so it stays verifiable in EditMode tests.
    /// </remarks>
    public static class SpinnerTextureFactory
    {
        /// <summary>
        /// Creates a square spinner texture of <paramref name="size"/> px (clamped to ≥ 8), tinted
        /// <paramref name="color"/> (defaults to white when null). Caller owns and must destroy it.
        /// </summary>
        public static Texture2D Create(int size, Color? color = null)
        {
            size = Mathf.Max(8, size);
            Color tint = color ?? Color.white;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "QrLoadingSpinner",
            };

            float center = (size - 1) * 0.5f;
            float outer = size * 0.5f - 1f;
            float inner = outer * 0.58f;
            float edge = Mathf.Max(1f, size * 0.03f); // soft anti-aliased band edges

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);

                    // Membership in the ring band, with soft inner/outer edges.
                    float radial = SmoothStep01(inner - edge, inner, radius)
                                   * (1f - SmoothStep01(outer, outer + edge, radius));

                    // Alpha fades from a faint tail to a solid head around the circle.
                    float angular = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI); // 0..1
                    float alpha = Mathf.Clamp01(radial * angular);

                    pixels[y * size + x] = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
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
