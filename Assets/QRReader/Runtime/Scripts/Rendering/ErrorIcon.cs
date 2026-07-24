using QRReader.Decoding;
using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// The shared error-state visual (architecture.md §3.7, §4; M4-T5): a static error icon shown on
    /// the content surface for <b>any</b> failure cause — resolve failure, timeout, or unsupported
    /// content type ("fail to the error state, never silently", §8). Renders a procedurally generated
    /// icon (<see cref="ErrorIconTextureFactory"/>) on the quad's transparent unlit material (M4-T1).
    /// </summary>
    /// <remarks>
    /// Structured like <see cref="LoadingSpinner"/> (owns its material instance and generated texture,
    /// freed on destroy — §7) but static: no rotation, since an error is a terminal state, not a
    /// progress indicator. The state machine that shows/hides it per QR is M4-T6; <see cref="SetVisible"/>
    /// is the seam it drives.
    /// </remarks>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ErrorIcon : MonoBehaviour
    {
        [SerializeField]
        [Min(8)]
        [Tooltip("Generated error-icon texture resolution, in pixels (square).")]
        private int _textureSize = 128;

        private MeshRenderer _meshRenderer;
        private Material _materialInstance;
        private Texture2D _iconTexture;

        private void Awake() => EnsureVisual();

        private void OnDestroy()
        {
            // Free the per-instance material and generated texture so the error visual leaks nothing.
            TextureCleanup.Destroy(_materialInstance);
            TextureCleanup.Destroy(_iconTexture);
        }

        /// <summary>Shows or hides the error icon.</summary>
        public void SetVisible(bool visible)
        {
            EnsureVisual();
            _meshRenderer.enabled = visible;
        }

        /// <summary>The generated icon texture (for verification/teardown); null until initialized.</summary>
        public Texture2D IconTexture => _iconTexture;

        private void EnsureVisual()
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            if (_iconTexture == null)
            {
                _iconTexture = ErrorIconTextureFactory.Create(_textureSize);
            }

            // Own an explicit material copy (rather than the auto-instancing `renderer.material` getter,
            // which warns/leaks in edit mode) so we don't mutate the shared quad material asset and can
            // free it deterministically on destroy.
            if (_materialInstance == null)
            {
                Material template = _meshRenderer.sharedMaterial;
                _materialInstance = template != null
                    ? new Material(template)
                    : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _materialInstance.name = "QrErrorIcon (Instance)";
                _materialInstance.mainTexture = _iconTexture; // URP Unlit _BaseMap is [MainTexture]
                _meshRenderer.sharedMaterial = _materialInstance;
            }
        }
    }
}
