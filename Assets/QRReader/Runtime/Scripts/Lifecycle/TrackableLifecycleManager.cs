using System;
using System.Collections.Generic;
using QRReader.DetectionSource;
using UnityEngine;

namespace QRReader.Lifecycle
{
    /// <summary>
    /// Owns the mapping from each tracked QR to its <see cref="TrackedQrEntry"/> — one entry per QR
    /// (architecture.md §3.3). Creates an entry when the <see cref="QrDetectionSource"/> reports a
    /// QR and removes it when the QR is lost.
    /// </summary>
    /// <remarks>
    /// This is the M1 skeleton: it maintains the dictionary only. Later milestones drive the per-QR
    /// pipeline (loading → resolve → classify → decode → render) from add, keep content aligned to
    /// the trackable pose while tracked, and destroy the content instance / free textures on removal
    /// (M5). Entries are keyed by the QR's <see cref="IQrCode.Pose"/> transform, which is stable for
    /// the QR's lifetime and identical across its detect/lost events.
    /// </remarks>
    [AddComponentMenu("QR Reader/Trackable Lifecycle Manager")]
    public sealed class TrackableLifecycleManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Detection source that raises QR detected/lost events. Defaults to a sibling on this GameObject.")]
        private QrDetectionSource detectionSource;

        private readonly Dictionary<Transform, TrackedQrEntry> _entries = new();

        /// <summary>Raised after a new QR entry is created.</summary>
        public event Action<TrackedQrEntry> EntryAdded;

        /// <summary>Raised after a QR entry is removed.</summary>
        public event Action<TrackedQrEntry> EntryRemoved;

        /// <summary>The number of QRs currently tracked.</summary>
        public int Count => _entries.Count;

        /// <summary>The current tracked-QR entries.</summary>
        public IReadOnlyCollection<TrackedQrEntry> Entries => _entries.Values;

        private void Awake()
        {
            if (detectionSource == null)
            {
                detectionSource = GetComponent<QrDetectionSource>();
            }
        }

        private void OnEnable()
        {
            if (detectionSource == null)
            {
                Debug.LogWarning(
                    $"{nameof(TrackableLifecycleManager)}: no {nameof(QrDetectionSource)} assigned; " +
                    "no QR detection events will be handled.", this);
                return;
            }

            detectionSource.QrCodeDetected += OnQrCodeDetected;
            detectionSource.QrCodeLost += OnQrCodeLost;
        }

        private void OnDisable()
        {
            if (detectionSource == null)
            {
                return;
            }

            detectionSource.QrCodeDetected -= OnQrCodeDetected;
            detectionSource.QrCodeLost -= OnQrCodeLost;
        }

        private void OnQrCodeDetected(IQrCode qrCode)
        {
            var key = qrCode.Pose;
            if (key == null)
            {
                Debug.LogWarning($"{nameof(TrackableLifecycleManager)}: detected QR has no pose; ignoring.", this);
                return;
            }

            if (_entries.ContainsKey(key))
            {
                // MRUK should not add the same trackable twice, but stay idempotent if it does.
                Debug.LogWarning($"{nameof(TrackableLifecycleManager)}: QR '{qrCode.Payload}' already tracked; ignoring duplicate add.", this);
                return;
            }

            var entry = new TrackedQrEntry(qrCode);
            _entries.Add(key, entry);
            EntryAdded?.Invoke(entry);
        }

        private void OnQrCodeLost(IQrCode qrCode)
        {
            var key = qrCode.Pose;
            if (key == null || !_entries.TryGetValue(key, out var entry))
            {
                // Unknown or already-removed QR — nothing to tear down.
                return;
            }

            _entries.Remove(key);
            EntryRemoved?.Invoke(entry);
        }
    }
}
