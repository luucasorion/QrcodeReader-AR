using System.Collections.Generic;
using QRReader.Configuration;
using QRReader.Decoding;
using QRReader.DetectionSource;
using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// The content instance's renderer and state API (architecture.md §3.7, §4; M4-T2/T3/T6). Lives on
    /// the content quad (M4-T1) and is the single object the lifecycle manager drives: it shows the
    /// loading spinner, the shared error icon, or the resolved media, and plays GIF frames. It places
    /// and sizes the quad at the QR's pose (<see cref="Fit"/>/<see cref="FitContent"/>, M4-T2/T3) and
    /// displays media on its own <see cref="MeshRenderer"/>.
    /// </summary>
    /// <remarks>
    /// The loading (<see cref="LoadingSpinner"/>) and error (<see cref="ErrorIcon"/>) visuals are child
    /// objects, discovered at runtime, so exactly one of {loading, error, media} is visible per state.
    /// It owns only its <b>media material instance</b> (freed on destroy, §7); the frame textures are
    /// owned by the <see cref="DecodedContent"/> the lifecycle passes in and frees on teardown (M5-T4).
    /// Pose-follow while tracked is M5-T2; the sizing/timing math is the pure, tested
    /// <see cref="ContentQuadFit"/> and <see cref="GifPlayback"/>.
    /// </remarks>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ContentRenderer : MonoBehaviour
    {
        /// <summary>Which feedback state the renderer is currently showing.</summary>
        public enum State
        {
            Hidden,
            Loading,
            Error,
            Media,
        }

        [SerializeField]
        [Tooltip("Render configuration (scale factor). If unset, the default scale factor is used.")]
        private ContentRendererConfig _config;

        private MeshRenderer _mediaRenderer;
        private Material _mediaMaterial;
        private LoadingSpinner _loadingSpinner;
        private ErrorIcon _errorIcon;

        private IReadOnlyList<GifFrame> _frames;
        private int[] _frameDelaysMs;
        private float _elapsedMs;
        private int _frameIndex;

        /// <summary>The scale factor in effect — from <see cref="_config"/>, or the default if unset.</summary>
        public float ScaleFactor =>
            _config != null ? _config.RenderScaleFactor : ContentRendererConfig.DefaultRenderScaleFactor;

        /// <summary>The current feedback state.</summary>
        public State CurrentState { get; private set; } = State.Hidden;

        /// <summary>The texture currently on the media surface (for verification); null when none.</summary>
        public Texture DisplayedTexture => _mediaMaterial != null ? _mediaMaterial.mainTexture : null;

        private void Awake()
        {
            EnsureInitialized();
            Hide();
        }

        private void Update()
        {
            // Advance an animated GIF; a still image (≤ 1 frame) needs no per-frame work.
            if (CurrentState != State.Media || _frames == null || _frames.Count <= 1)
            {
                return;
            }

            _elapsedMs += Time.deltaTime * 1000f;
            int index = GifPlayback.FrameIndexAt(_elapsedMs, _frameDelaysMs);
            if (index != _frameIndex)
            {
                _frameIndex = index;
                _mediaMaterial.mainTexture = _frames[index].Texture;
            }
        }

        private void OnDestroy() => TextureCleanup.Destroy(_mediaMaterial); // frames belong to DecodedContent

        // --- State API (driven by the lifecycle manager, M5-T1) --------------

        /// <summary>Hides all visuals (the initial state before content is requested).</summary>
        public void Hide()
        {
            EnsureInitialized();
            StopPlayback();
            _mediaRenderer.enabled = false;
            _loadingSpinner?.SetVisible(false);
            _errorIcon?.SetVisible(false);
            CurrentState = State.Hidden;
        }

        /// <summary>Shows the loading spinner for <paramref name="qrCode"/>, sized to the QR plane.</summary>
        public void ShowLoading(IQrCode qrCode)
        {
            EnsureInitialized();
            StopPlayback();
            Fit(qrCode);
            _mediaRenderer.enabled = false;
            _errorIcon?.SetVisible(false);
            _loadingSpinner?.SetVisible(true);
            CurrentState = State.Loading;
        }

        /// <summary>
        /// Shows the shared error icon for <paramref name="qrCode"/> — the single visual for every
        /// failure cause (resolve failure, timeout, unsupported type; §4, §8).
        /// </summary>
        public void ShowError(IQrCode qrCode)
        {
            EnsureInitialized();
            StopPlayback();
            Fit(qrCode);
            _mediaRenderer.enabled = false;
            _loadingSpinner?.SetVisible(false);
            _errorIcon?.SetVisible(true);
            CurrentState = State.Error;
        }

        /// <summary>
        /// Shows the resolved <paramref name="content"/> for <paramref name="qrCode"/>: sizes the quad
        /// letterboxed to the content aspect (M4-T3) and, for an animated GIF, plays its frames. Returns
        /// false (leaving the current state) when the content is empty or the QR has no pose/plane yet.
        /// </summary>
        public bool ShowMedia(IQrCode qrCode, DecodedContent content)
        {
            EnsureInitialized();

            if (content == null || content.FrameCount == 0)
            {
                return false;
            }

            Texture firstFrame = content.Frames[0].Texture;
            if (!FitContent(qrCode, firstFrame))
            {
                return false;
            }

            _loadingSpinner?.SetVisible(false);
            _errorIcon?.SetVisible(false);

            _frames = content.Frames;
            _frameDelaysMs = new int[_frames.Count];
            for (int i = 0; i < _frames.Count; i++)
            {
                _frameDelaysMs[i] = _frames[i].DelayMs;
            }

            _elapsedMs = 0f;
            _frameIndex = 0;
            _mediaMaterial.mainTexture = firstFrame;
            _mediaRenderer.enabled = true;
            CurrentState = State.Media;
            return true;
        }

        private void EnsureInitialized()
        {
            if (_mediaRenderer == null)
            {
                _mediaRenderer = GetComponent<MeshRenderer>();
            }

            // Own an explicit material copy (not the auto-instancing `renderer.material`, which
            // warns/leaks in edit mode) so we don't mutate the shared quad material asset (M4-T1).
            if (_mediaMaterial == null)
            {
                Material template = _mediaRenderer.sharedMaterial;
                _mediaMaterial = template != null
                    ? new Material(template)
                    : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _mediaMaterial.name = "QrContent (Instance)";
                _mediaRenderer.sharedMaterial = _mediaMaterial;
            }

            // Children may be absent in a bare/test setup; the state API guards for null.
            if (_loadingSpinner == null)
            {
                _loadingSpinner = GetComponentInChildren<LoadingSpinner>(includeInactive: true);
            }

            if (_errorIcon == null)
            {
                _errorIcon = GetComponentInChildren<ErrorIcon>(includeInactive: true);
            }
        }

        private void StopPlayback()
        {
            _frames = null;
            _frameDelaysMs = null;
            _elapsedMs = 0f;
            _frameIndex = 0;
        }

        // --- Placement / sizing (M4-T2 / M4-T3) ------------------------------

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
