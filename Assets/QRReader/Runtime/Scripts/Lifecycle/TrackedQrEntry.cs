using QRReader.DetectionSource;

namespace QRReader.Lifecycle
{
    /// <summary>
    /// One tracked QR code's bookkeeping entry, owned by the <see cref="TrackableLifecycleManager"/>
    /// (architecture.md §3.3 — one entry per QR).
    /// </summary>
    /// <remarks>
    /// This is the M1 skeleton: it holds only the <see cref="IQrCode"/>. Later milestones extend
    /// the entry with the per-QR content instance and its owned textures (M5), which are created on
    /// add and released on removal.
    /// </remarks>
    public sealed class TrackedQrEntry
    {
        public TrackedQrEntry(IQrCode qrCode)
        {
            QrCode = qrCode;
        }

        /// <summary>The detected QR this entry represents. Pose/PlaneRect/IsTracked read live.</summary>
        public IQrCode QrCode { get; }
    }
}
