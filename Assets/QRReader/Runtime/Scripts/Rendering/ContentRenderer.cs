using QRReader.Configuration;
using QRReader.DetectionSource;
using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// Places and sizes the content quad (M4-T1 prefab) flush/coplanar at a QR's pose, sized from its
    /// <c>PlaneRect</c> × the configured render scale factor (architecture.md §3.7, M4-T2). Lives on the
    /// quad prefab root and drives its own <see cref="Transform"/>.
    /// </summary>
    /// <remarks>
    /// M4-T2 covers placement/sizing only. Keeping the quad aligned to the (low-frequency) trackable
    /// pose each update is M5-T2; letterboxing non-square textures within the quad is M4-T3; the
    /// loading/error/media state API is M4-T6. If no <see cref="ContentRendererConfig"/> is assigned it
    /// falls back to <see cref="ContentRendererConfig.DefaultRenderScaleFactor"/> so the quad still
    /// sizes sensibly. The sizing math itself is the pure, tested <see cref="ContentQuadFit"/>.
    /// </remarks>
    public sealed class ContentRenderer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Render configuration (scale factor). If unset, the default scale factor is used.")]
        private ContentRendererConfig _config;

        /// <summary>The scale factor in effect — from <see cref="_config"/>, or the default if unset.</summary>
        public float ScaleFactor =>
            _config != null ? _config.RenderScaleFactor : ContentRendererConfig.DefaultRenderScaleFactor;

        /// <summary>
        /// Places and sizes the quad for <paramref name="qrCode"/> using its live pose and plane. A QR
        /// with no reported <c>PlaneRect</c> yet is left unsized (returns false) so the caller can wait
        /// for the runtime to report one rather than collapse the quad to zero.
        /// </summary>
        public bool Fit(IQrCode qrCode)
        {
            if (qrCode == null || qrCode.Pose == null || !qrCode.PlaneRect.HasValue)
            {
                return false;
            }

            Transform pose = qrCode.Pose;
            Fit(pose.position, pose.rotation, qrCode.PlaneRect.Value);
            return true;
        }

        /// <summary>
        /// Places and sizes the quad at <paramref name="position"/>/<paramref name="rotation"/> from
        /// <paramref name="planeRect"/> × the configured scale factor.
        /// </summary>
        public void Fit(Vector3 position, Quaternion rotation, Rect planeRect) =>
            Fit(position, rotation, planeRect, ScaleFactor);

        /// <summary>
        /// Placement/sizing with an explicit <paramref name="scaleFactor"/> (bypasses config). The seam
        /// the EditMode tests drive so the transform result can be asserted without a config asset.
        /// </summary>
        public void Fit(Vector3 position, Quaternion rotation, Rect planeRect, float scaleFactor)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = ContentQuadFit.ComputeLocalScale(planeRect, scaleFactor);
        }

        /// <summary>
        /// Places the quad at <paramref name="qrCode"/>'s pose and sizes it to <paramref name="content"/>
        /// letterboxed within the scaled QR plane, so a non-square texture keeps its aspect ratio
        /// (M4-T3). Returns false (leaving the quad untouched) when the QR has no pose/plane yet or the
        /// texture is null. This is the sizing the media state uses (M4-T6).
        /// </summary>
        public bool FitContent(IQrCode qrCode, Texture content)
        {
            if (qrCode == null || qrCode.Pose == null || !qrCode.PlaneRect.HasValue || content == null)
            {
                return false;
            }

            Transform pose = qrCode.Pose;
            FitContent(pose.position, pose.rotation, qrCode.PlaneRect.Value, content.width, content.height);
            return true;
        }

        /// <summary>
        /// Places and sizes the quad at <paramref name="position"/>/<paramref name="rotation"/>, fitting
        /// content of size <paramref name="contentWidth"/>×<paramref name="contentHeight"/> letterboxed
        /// within <paramref name="planeRect"/> × the configured scale factor.
        /// </summary>
        public void FitContent(
            Vector3 position, Quaternion rotation, Rect planeRect, float contentWidth, float contentHeight) =>
            FitContent(position, rotation, planeRect, ScaleFactor, contentWidth, contentHeight);

        /// <summary>
        /// Letterboxed placement/sizing with an explicit <paramref name="scaleFactor"/> (bypasses
        /// config). The seam the EditMode tests drive so the transform result can be asserted without a
        /// config asset or a real texture.
        /// </summary>
        public void FitContent(
            Vector3 position, Quaternion rotation, Rect planeRect, float scaleFactor,
            float contentWidth, float contentHeight)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = ContentQuadFit.ComputeLetterboxedLocalScale(
                planeRect, scaleFactor, contentWidth, contentHeight);
        }
    }
}
