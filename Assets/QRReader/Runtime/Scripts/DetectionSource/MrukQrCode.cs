using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace QRReader.DetectionSource
{
    /// <summary>
    /// Adapts a <see cref="MRUKTrackable"/> to <see cref="IQrCode"/>. This is the single place
    /// MRUK trackable types are read; everything downstream sees only <see cref="IQrCode"/>
    /// (architecture.md §3.2). All members pass through live to the wrapped trackable.
    /// </summary>
    internal sealed class MrukQrCode : IQrCode
    {
        private readonly MRUKTrackable _trackable;

        public MrukQrCode(MRUKTrackable trackable)
        {
            _trackable = trackable;
        }

        public string Payload => _trackable.MarkerPayloadString;

        public Transform Pose => _trackable.transform;

        public Rect? PlaneRect => _trackable.PlaneRect;

        public bool IsTracked => _trackable.IsTracked;
    }
}
