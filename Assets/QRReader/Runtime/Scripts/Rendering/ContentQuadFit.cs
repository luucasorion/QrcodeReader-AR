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
    /// world-space size in metres maps directly to <see cref="Transform.localScale"/>.
    /// <see cref="ComputeLocalScale"/> sizes the quad to the QR plane (the bounding box the loading /
    /// error visuals fill); <see cref="ComputeLetterboxedLocalScale"/> fits a non-square texture's
    /// aspect ratio <i>within</i> that box (M4-T3), leaving the remainder transparent over passthrough
    /// so the content is never stretched.
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

        /// <summary>
        /// The quad's local scale for content of size <paramref name="contentWidth"/>×
        /// <paramref name="contentHeight"/> (e.g. a decoded texture) letterboxed within the scaled QR
        /// plane: the largest rectangle with the content's aspect ratio that fits inside the bounding
        /// box from <see cref="ComputeLocalScale"/>, so the content keeps its shape and is never
        /// stretched. Degenerate content dimensions (≤ 0) fall back to filling the box.
        /// </summary>
        public static Vector3 ComputeLetterboxedLocalScale(
            Rect planeRect, float scaleFactor, float contentWidth, float contentHeight)
        {
            Vector3 box = ComputeLocalScale(planeRect, scaleFactor);
            return FitWithinBox(box, contentWidth, contentHeight);
        }

        /// <summary>
        /// The largest rectangle with the content's aspect ratio ("contain" fit) that fits inside a
        /// <paramref name="box"/> (x/y in metres). Returns the box unchanged when either the box or the
        /// content has no usable (positive) dimension.
        /// </summary>
        internal static Vector3 FitWithinBox(Vector3 box, float contentWidth, float contentHeight)
        {
            if (box.x <= 0f || box.y <= 0f || contentWidth <= 0f || contentHeight <= 0f)
            {
                return box;
            }

            float contentAspect = contentWidth / contentHeight;
            float boxAspect = box.x / box.y;

            // Content relatively wider than the box → constrain width (letterbox top/bottom);
            // otherwise constrain height (pillarbox left/right). Equal aspect fills the box exactly.
            if (contentAspect >= boxAspect)
            {
                return new Vector3(box.x, box.x / contentAspect, 1f);
            }

            return new Vector3(box.y * contentAspect, box.y, 1f);
        }
    }
}
