using QRReader.Decoding;
using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// The loading-state visual (architecture.md §3.7, M4-T4): a spinner shown on the content surface
    /// while a detected QR's content is being resolved/decoded, before the image/GIF or error visual
    /// takes over. Renders a procedurally generated ring (<see cref="SpinnerTextureFactory"/>) on the
    /// quad's transparent unlit material (M4-T1) and rotates it in-plane.
    /// </summary>
    /// <remarks>
    /// Lives on a quad (MeshFilter + MeshRenderer). It instances its own material and owns the
    /// generated texture, freeing both on destroy so the loading visual leaks nothing (§7) — the same
    /// ownership discipline as the decoded content. The state machine that shows/hides it per QR is
    /// M4-T6; <see cref="SetVisible"/> is the seam it drives. Rotation is the pure, tested
    /// <see cref="SpinnerRotation"/>.
    /// </remarks>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class LoadingSpinner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Spin speed in degrees per second. Negative spins the other way.")]
        private float _degreesPerSecond = 180f;

        [SerializeField]
        [Min(8)]
        [Tooltip("Generated spinner texture resolution, in pixels (square).")]
        private int _textureSize = 128;

        private MeshRenderer _meshRenderer;
        private Material _materialInstance;
        private Texture2D _spinnerTexture;
        private float _elapsedSeconds;

        private void Awake() => EnsureVisual();

        private void OnEnable()
        {
            // Restart the sweep each time it's shown so it always begins from a consistent pose.
            _elapsedSeconds = 0f;
            ApplyRotation();
        }

        private void Update()
        {
            _elapsedSeconds += Time.deltaTime;
            ApplyRotation();
        }

        private void OnDestroy()
        {
            // Free the per-instance material and generated texture so the loading visual leaks nothing.
            TextureCleanup.Destroy(_materialInstance);
            TextureCleanup.Destroy(_spinnerTexture);
        }

        /// <summary>Shows or hides the spinner; showing restarts its sweep from the start.</summary>
        public void SetVisible(bool visible)
        {
            EnsureVisual();
            _meshRenderer.enabled = visible;
            if (visible)
            {
                _elapsedSeconds = 0f;
                ApplyRotation();
            }
        }

        /// <summary>The generated spinner texture (for verification/teardown); null until initialized.</summary>
        public Texture2D SpinnerTexture => _spinnerTexture;

        private void EnsureVisual()
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            if (_spinnerTexture == null)
            {
                _spinnerTexture = SpinnerTextureFactory.Create(_textureSize);
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
                _materialInstance.name = "QrLoadingSpinner (Instance)";
                _materialInstance.mainTexture = _spinnerTexture; // URP Unlit _BaseMap is [MainTexture]
                _meshRenderer.sharedMaterial = _materialInstance;
            }
        }

        private void ApplyRotation()
        {
            float angle = SpinnerRotation.AngleDegrees(_elapsedSeconds, _degreesPerSecond);
            // Spin around the quad's facing axis so the ring rotates in its own plane.
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
