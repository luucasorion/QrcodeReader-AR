using UnityEngine;

namespace QRReader.Decoding
{
    /// <summary>
    /// Shared texture teardown for the decode path. <see cref="UnityEngine.Object.Destroy"/> is the
    /// runtime path; <see cref="UnityEngine.Object.DestroyImmediate"/> is required in edit mode (e.g.
    /// EditMode tests), where <c>Destroy</c> is disallowed. Centralized so the decoders and the
    /// decoded-content handle free textures the same, leak-free way in both contexts (§7).
    /// </summary>
    internal static class TextureCleanup
    {
        public static void Destroy(Object obj)
        {
            if (obj == null)
            {
                return;
            }

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
