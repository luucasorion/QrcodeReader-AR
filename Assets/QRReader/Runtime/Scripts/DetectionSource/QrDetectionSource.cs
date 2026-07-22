using System;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace QRReader.DetectionSource
{
    /// <summary>
    /// Bridges MRUK trackable detection to the rest of the app (architecture.md §3.2, ADR 0002).
    /// Subscribes to MRUK's <c>TrackableAdded</c>/<c>TrackableRemoved</c> events, filters to
    /// <see cref="OVRAnchor.TrackableType.QRCode"/>, and re-raises them as <see cref="IQrCode"/>
    /// events so nothing downstream depends on MRUK types.
    /// </summary>
    /// <remarks>
    /// This component does not decode camera pixels — pose, payload, and lifecycle come first-party
    /// from MRUK. It is the only place MRUK trackable events are consumed; keep all detection here.
    /// </remarks>
    [AddComponentMenu("QR Reader/QR Detection Source")]
    public sealed class QrDetectionSource : MonoBehaviour
    {
        /// <summary>Raised when a QR code is detected and localized by the runtime.</summary>
        public event Action<IQrCode> QrCodeDetected;

        /// <summary>Raised when a previously detected QR code is no longer tracked by the runtime.</summary>
        public event Action<IQrCode> QrCodeLost;

        private MRUK _mruk;
        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        // MRUK.Instance may not exist yet in OnEnable depending on script execution order;
        // retry in Start once the scene's MRUK singleton is guaranteed to have initialized.
        private void Start() => TrySubscribe();

        private void OnDisable() => Unsubscribe();

        private void TrySubscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _mruk = MRUK.Instance;
            if (_mruk == null)
            {
                return;
            }

            _mruk.SceneSettings.TrackableAdded.AddListener(HandleTrackableAdded);
            _mruk.SceneSettings.TrackableRemoved.AddListener(HandleTrackableRemoved);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _mruk == null)
            {
                return;
            }

            _mruk.SceneSettings.TrackableAdded.RemoveListener(HandleTrackableAdded);
            _mruk.SceneSettings.TrackableRemoved.RemoveListener(HandleTrackableRemoved);
            _subscribed = false;
        }

        private void HandleTrackableAdded(MRUKTrackable trackable)
        {
            if (!IsQrCode(trackable))
            {
                return;
            }

            QrCodeDetected?.Invoke(new MrukQrCode(trackable));
        }

        private void HandleTrackableRemoved(MRUKTrackable trackable)
        {
            if (!IsQrCode(trackable))
            {
                return;
            }

            QrCodeLost?.Invoke(new MrukQrCode(trackable));
        }

        private static bool IsQrCode(MRUKTrackable trackable) =>
            trackable != null && trackable.TrackableType == OVRAnchor.TrackableType.QRCode;
    }
}
