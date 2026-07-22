using UnityEngine;

namespace QRReader.DetectionSource
{
    /// <summary>
    /// A detected QR code surfaced by the <see cref="QrDetectionSource"/>, decoupled from the
    /// underlying MRUK types so the rest of the app never depends on Meta XR directly
    /// (architecture.md §3.2, §8 "keep boundaries clean").
    /// </summary>
    /// <remarks>
    /// <see cref="Pose"/>, <see cref="PlaneRect"/>, and <see cref="IsTracked"/> read live from the
    /// underlying trackable, so a held reference reflects the QR's current state each time it is
    /// read. <see cref="Pose"/> is stable for the lifetime of a QR and can be used as an identity
    /// key by the lifecycle manager (M1-T2). Note MRUK pose updates are low-frequency (ADR 0002).
    /// </remarks>
    public interface IQrCode
    {
        /// <summary>The QR payload as a string — an <c>https://</c> URL in this app (ADR 0003).</summary>
        string Payload { get; }

        /// <summary>The trackable's transform (real-world pose). Stable for the QR's lifetime.</summary>
        Transform Pose { get; }

        /// <summary>The QR's physical plane size, or <c>null</c> if the runtime has not reported one.</summary>
        Rect? PlaneRect { get; }

        /// <summary>Whether the runtime currently considers this QR tracked.</summary>
        bool IsTracked { get; }
    }
}
