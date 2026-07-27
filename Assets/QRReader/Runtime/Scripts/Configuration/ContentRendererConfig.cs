using UnityEngine;

namespace QRReader.Configuration
{
    /// <summary>
    /// Serialized configuration for the content renderer (architecture.md §3.7, §3.8). Currently the
    /// <b>render scale factor</b>: the QR content quad is sized from the QR's <c>PlaneRect</c> scaled
    /// up by this factor (project context — "scaled up by a configurable factor"; exact-QR-size
    /// rendering was rejected). Kept in its own asset, parallel to <see cref="ContentResolverConfig"/>,
    /// so each system owns its configuration (§8 "keep boundaries clean").
    /// </summary>
    /// <remarks>
    /// The full configuration surface (this value plus the resolver guards) is documented in
    /// <c>docs/configuration.md</c> (finalized in M6-T1). The authored asset lives at
    /// <c>Assets/QRReader/Runtime/Config/ContentRendererConfig.asset</c> and is wired into the
    /// <c>QrContentInstance</c> prefab's renderer; create a new one via
    /// <c>Assets ▸ Create ▸ QR Reader ▸ Content Renderer Config</c> if needed.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ContentRendererConfig",
        menuName = "QR Reader/Content Renderer Config",
        order = 1)]
    public sealed class ContentRendererConfig : ScriptableObject
    {
        /// <summary>
        /// Default render scale factor: the content quad is drawn 3× the physical QR size so it's
        /// comfortably readable at arm's length (M6-T1 default; see docs/configuration.md).
        /// </summary>
        public const float DefaultRenderScaleFactor = 3f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("Multiplier applied to the QR's PlaneRect to size the content quad. 1 = exact QR " +
                 "size; larger scales the content up. Never below 1 (content is scaled up, not down).")]
        private float _renderScaleFactor = DefaultRenderScaleFactor;

        /// <summary>Multiplier applied to the QR's <c>PlaneRect</c> to size the quad (always ≥ 1).</summary>
        public float RenderScaleFactor => _renderScaleFactor;

        // Clamp so an asset edited to below 1 can't shrink content below the physical QR size.
        private void OnValidate()
        {
            if (_renderScaleFactor < 1f)
            {
                _renderScaleFactor = 1f;
            }
        }
    }
}
