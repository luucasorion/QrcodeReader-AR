using QRReader.Decoding;
using QRReader.DetectionSource;
using QRReader.Rendering;

namespace QRReader.Lifecycle
{
    /// <summary>
    /// One tracked QR code's bookkeeping entry, owned by the <see cref="TrackableLifecycleManager"/>
    /// (architecture.md §3.3 — one entry, and one content instance, per QR).
    /// </summary>
    /// <remarks>
    /// Beyond the detected <see cref="IQrCode"/> it holds the per-QR pipeline's outputs (M5-T1): the
    /// <see cref="Renderer"/> content instance that shows the loading/error/media state, and the
    /// <see cref="Content"/> handle that owns any decoded texture(s). Both are created on add and
    /// released on removal (destroy + texture free land in M5-T4). <see cref="IsRetired"/> lets the
    /// asynchronous pipeline abandon its result if the QR is lost while a download is still in flight.
    /// </remarks>
    public sealed class TrackedQrEntry
    {
        public TrackedQrEntry(IQrCode qrCode)
        {
            QrCode = qrCode;
        }

        /// <summary>The detected QR this entry represents. Pose/PlaneRect/IsTracked read live.</summary>
        public IQrCode QrCode { get; }

        /// <summary>
        /// The content instance driving this QR's feedback visuals (loading → error → media).
        /// Instantiated by the <see cref="TrackableLifecycleManager"/> on add; null when no content
        /// prefab is configured. Destroyed on removal (M5-T4).
        /// </summary>
        public ContentRenderer Renderer { get; internal set; }

        /// <summary>
        /// The decoded content this entry owns once media decodes — null while loading, on any failure,
        /// or for unsupported content. Held so its texture(s) can be freed on teardown (§7, M5-T4).
        /// </summary>
        public DecodedContent Content { get; internal set; }

        /// <summary>
        /// True once the QR has been lost and this entry retired. The per-QR pipeline runs
        /// asynchronously (the resolver awaits a network download), so it may still be mid-flight when
        /// the QR is removed; the pipeline checks this to abandon its result instead of driving a
        /// torn-down instance.
        /// </summary>
        public bool IsRetired { get; internal set; }
    }
}
