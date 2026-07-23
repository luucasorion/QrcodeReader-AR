using System.Text;
using QRReader.Lifecycle;
using UnityEngine;

namespace QRReader.Debugging
{
    /// <summary>
    /// Temporary M1 diagnostics: logs and displays the payload, pose, and physical size of each
    /// detected QR code (implementation-plan M1-T3). There is no content pipeline yet — this exists
    /// only to confirm on-device that MRUK detection and the <see cref="TrackableLifecycleManager"/>
    /// add/remove lifecycle work (M1-T4).
    /// </summary>
    /// <remarks>
    /// This component is scheduled for removal in <c>M5-T5</c> once the real renderer replaces it; it
    /// is deliberately self-contained (no scene wiring beyond the manager reference) so it can be
    /// deleted without touching anything else. Logging is the primary channel because QR testing is
    /// on-device only (architecture.md §7) — read it via <c>adb logcat</c>. The on-screen IMGUI
    /// overlay is a convenience for the Editor / desktop mirror.
    /// </remarks>
    [AddComponentMenu("QR Reader/Debug/QR Debug Overlay (temporary, M1)")]
    public sealed class QrDebugOverlay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Lifecycle manager to observe. Defaults to a sibling on this GameObject.")]
        private TrackableLifecycleManager lifecycleManager;

        [SerializeField]
        [Tooltip("Log each QR's live pose/size on this interval (seconds) while tracked. 0 disables periodic logging.")]
        private float livePollInterval = 2f;

        [SerializeField]
        [Tooltip("Draw the on-screen IMGUI overlay (Editor / desktop mirror). Logging happens regardless.")]
        private bool drawOverlay = true;

        private float _nextPollTime;

        private void Awake()
        {
            if (lifecycleManager == null)
            {
                lifecycleManager = GetComponent<TrackableLifecycleManager>();
            }
        }

        private void OnEnable()
        {
            if (lifecycleManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(QrDebugOverlay)}: no {nameof(TrackableLifecycleManager)} assigned; " +
                    "nothing to report.", this);
                return;
            }

            lifecycleManager.EntryAdded += OnEntryAdded;
            lifecycleManager.EntryRemoved += OnEntryRemoved;
        }

        private void OnDisable()
        {
            if (lifecycleManager == null)
            {
                return;
            }

            lifecycleManager.EntryAdded -= OnEntryAdded;
            lifecycleManager.EntryRemoved -= OnEntryRemoved;
        }

        private void Update()
        {
            if (lifecycleManager == null || livePollInterval <= 0f || lifecycleManager.Count == 0)
            {
                return;
            }

            if (Time.unscaledTime < _nextPollTime)
            {
                return;
            }

            _nextPollTime = Time.unscaledTime + livePollInterval;

            foreach (var entry in lifecycleManager.Entries)
            {
                Debug.Log($"[QR] tracking — {Describe(entry)}", this);
            }
        }

        private void OnEntryAdded(TrackedQrEntry entry) =>
            Debug.Log($"[QR] detected — {Describe(entry)}", this);

        private void OnEntryRemoved(TrackedQrEntry entry) =>
            Debug.Log($"[QR] lost — {Describe(entry)}", this);

        // Single description used by both logging and the overlay so they never drift.
        private static string Describe(TrackedQrEntry entry)
        {
            var qr = entry.QrCode;
            var pose = qr.Pose;
            var position = pose != null ? pose.position.ToString("F3") : "(no pose)";
            var rotation = pose != null ? pose.rotation.eulerAngles.ToString("F1") : "(no pose)";
            var size = qr.PlaneRect is { } rect
                ? $"{rect.width:F3}m x {rect.height:F3}m"
                : "(no size)";
            var payload = string.IsNullOrEmpty(qr.Payload) ? "(empty)" : qr.Payload;

            return $"payload='{payload}' pos={position} rotEuler={rotation} size={size} tracked={qr.IsTracked}";
        }

        private void OnGUI()
        {
            if (!drawOverlay || lifecycleManager == null)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"QR Debug (M1) — tracked: {lifecycleManager.Count}");
            foreach (var entry in lifecycleManager.Entries)
            {
                sb.AppendLine($"• {Describe(entry)}");
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                wordWrap = true,
            };
            GUI.Label(new Rect(16, 16, 720, 32 + lifecycleManager.Count * 48), sb.ToString(), style);
        }
    }
}
