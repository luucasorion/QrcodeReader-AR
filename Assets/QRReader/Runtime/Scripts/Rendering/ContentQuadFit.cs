using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// Pure placement/sizing math for the content quad (architecture.md §3.7, M4-T2): where and how
    /// large the flush/coplanar quad sits, derived from the QR's pose and <c>PlaneRect</c> scaled up by
    /// a configurable factor. Split out from <see cref="ContentRenderer"/> — which touches a
    /// <see cref="Transform"/> — so the geometry stays EditMode-unit-testable off device (plan R1, §8).
    /// </summary>
    /// <remarks>
    /// The quad prefab (M4-T1) uses Unity's built-in Quad mesh, which is 1×1 unit in local space, so a
    /// world-space size in metres maps directly to <see cref="Transform.localScale"/>. Fitting a
    /// non-square texture <i>within</i> this quad (letterboxing) is a separate concern (M4-T3); this
    /// only sizes the quad itself to the QR plane.
    /// </remarks>
    public static class ContentQuadFit
    {
        /// <summary>
        /// The quad's local scale for a QR whose physical plane is <paramref name="planeRect"/>, drawn
        /// at <paramref name="scaleFactor"/>× its size. X/Y are the scaled plane extents in metres; Z
        /// is 1 (the quad is flat). <c>PlaneRect</c> dimensions are treated as magnitudes so a mirrored
        /// rect can't produce a negative (inside-out) scale.
        /// </summary>
        public static Vector3 ComputeLocalScale(Rect planeRect, float scaleFactor)
        {
            float factor = Mathf.Max(0f, scaleFactor);
            float width = Mathf.Abs(planeRect.width) * factor;
            float height = Mathf.Abs(planeRect.height) * factor;
            return new Vector3(width, height, 1f);
        }
    }
}
