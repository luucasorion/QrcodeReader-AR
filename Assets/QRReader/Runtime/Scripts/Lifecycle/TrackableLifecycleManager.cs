using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QRReader.Classification;
using QRReader.Configuration;
using QRReader.Decoding;
using QRReader.DetectionSource;
using QRReader.Rendering;
using QRReader.Resolver;
using UnityEngine;

namespace QRReader.Lifecycle
{
    /// <summary>
    /// Owns the mapping from each tracked QR to its <see cref="TrackedQrEntry"/> — one entry, and one
    /// content instance, per QR (architecture.md §3.3). Creates an entry when the
    /// <see cref="QrDetectionSource"/> reports a QR and removes it when the QR is lost, and drives that
    /// QR's content pipeline.
    /// </summary>
    /// <remarks>
    /// M5-T1 wires the per-QR pipeline (§4, §6): on add it instantiates a content instance, shows the
    /// loading state, then resolves → classifies → decodes → renders the payload; any stage that fails
    /// short-circuits to the shared error visual ("fail to the error state, never silently", §8). The
    /// resolver awaits a network download, so the pipeline runs asynchronously per QR; each entry is
    /// independent, so one QR's pipeline neither blocks nor affects another's (§4 per-QR isolation).
    /// N QRs are supported simultaneously with no cross-QR shared state (M5-T3): the per-QR mutable
    /// state (renderer instance, decoded content, retirement flag) all lives on the <see cref="TrackedQrEntry"/>,
    /// and the one shared collaborator — the <see cref="IContentResolver"/> — is reentrant (each
    /// <see cref="IContentResolver.GetAsync"/> owns its own request), so concurrent pipelines never
    /// interfere. Keeping the content instance aligned to the pose while tracked (M5-T2) and destroying the
    /// instance / freeing its textures on removal (M5-T4) build on this. Entries are keyed by the QR's
    /// <see cref="IQrCode.Pose"/> transform, which is stable for the QR's lifetime and identical across
    /// its detect/lost events.
    /// </remarks>
    [AddComponentMenu("QR Reader/Trackable Lifecycle Manager")]
    public sealed class TrackableLifecycleManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Detection source that raises QR detected/lost events. Defaults to a sibling on this GameObject.")]
        private QrDetectionSource detectionSource;

        [SerializeField]
        [Tooltip("Content instance prefab (a ContentRenderer with its loading/error visuals) instantiated per QR. " +
                 "Without it, QRs are still tracked but no content is shown.")]
        private ContentRenderer contentInstancePrefab;

        [SerializeField]
        [Tooltip("Resolver guard configuration (HTTPS-only, timeout, download cap). Without it, every QR " +
                 "routes to the error state because the payload cannot be resolved.")]
        private ContentResolverConfig resolverConfig;

        private readonly Dictionary<Transform, TrackedQrEntry> _entries = new();

        private IContentResolver _resolver;

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

            if (resolverConfig != null)
            {
                _resolver = new ContentResolver(resolverConfig);
            }
            else
            {
                Debug.LogWarning(
                    $"{nameof(TrackableLifecycleManager)}: no {nameof(ContentResolverConfig)} assigned; " +
                    "QR payloads cannot be resolved and every QR will show the error state.", this);
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

            // Start this QR's independent pipeline (§4): loading → resolve → classify → decode → render.
            BeginPipeline(entry);
        }

        private void OnQrCodeLost(IQrCode qrCode)
        {
            var key = qrCode.Pose;
            if (key == null || !_entries.TryGetValue(key, out var entry))
            {
                // Unknown or already-removed QR — nothing to tear down.
                return;
            }

            // Retire before removing so an in-flight pipeline (still awaiting the download) abandons its
            // result rather than driving this instance. Destroying the content instance and freeing
            // entry.Content's textures is M5-T4.
            entry.IsRetired = true;
            _entries.Remove(key);
            EntryRemoved?.Invoke(entry);
        }

        // Synchronous half of the pipeline: create the content instance and show loading immediately,
        // then hand off to the asynchronous resolve → classify → decode → render tail. Split so the
        // loading state appears the instant the QR is detected, before the network download begins (§6).
        private void BeginPipeline(TrackedQrEntry entry)
        {
            if (contentInstancePrefab == null)
            {
                Debug.LogWarning(
                    $"{nameof(TrackableLifecycleManager)}: no content instance prefab assigned; " +
                    $"tracking QR '{entry.QrCode.Payload}' without content.", this);
                return;
            }

            ContentRenderer renderer = Instantiate(contentInstancePrefab);
            renderer.name = $"QrContent ({entry.QrCode.Payload})";
            entry.Renderer = renderer;

            renderer.ShowLoading(entry.QrCode);

            // Fire-and-forget: the download awaits the network, and the continuation re-checks the entry
            // (IsRetired / a destroyed renderer) before touching a possibly-removed instance.
            _ = ResolveClassifyDecodeRenderAsync(entry);
        }

        private async Task ResolveClassifyDecodeRenderAsync(TrackedQrEntry entry)
        {
            IQrCode qr = entry.QrCode;
            ContentRenderer renderer = entry.Renderer;

            if (_resolver == null)
            {
                Debug.LogWarning(
                    $"{nameof(TrackableLifecycleManager)}: cannot resolve '{qr.Payload}' " +
                    "(no resolver config); showing the error state.", this);
                ShowErrorIfLive(entry);
                return;
            }

            // Resolve (download under the safety guards). The resolver never throws into the pipeline —
            // any violation/failure is a value we branch on (§3.4).
            ResolveResult resolved = await _resolver.GetAsync(qr.Payload);
            if (!IsLive(entry))
            {
                return; // QR lost while downloading — abandon the result (teardown is M5-T4).
            }

            if (!resolved.Success)
            {
                renderer.ShowError(qr);
                return;
            }

            // Classify (extension hint first, Content-Type fallback) then decode. Unsupported and
            // undecodable both surface as a DecodeResult failure → the shared error visual (§3.5, §3.6).
            ContentKind kind = ContentTypeClassifier.Classify(qr.Payload, resolved);
            DecodeResult decoded = MediaDecoder.Decode(kind, resolved.Bytes);

            if (!IsLive(entry))
            {
                // Lost during/after the (synchronous) decode: free any textures we just produced so a
                // removed QR doesn't leak them (until M5-T4 owns teardown).
                decoded.Content?.Dispose();
                return;
            }

            if (!decoded.Success)
            {
                renderer.ShowError(qr);
                return;
            }

            // Ownership of the decoded textures passes to the entry; freed on teardown (§7, M5-T4).
            entry.Content = decoded.Content;

            if (!renderer.ShowMedia(qr, decoded.Content))
            {
                // Decoded fine, but the QR reports no pose/plane to place the quad. Rather than leave a
                // blank/loading instance silently, surface the error visual (§8). Re-fitting once the
                // pose/plane arrives is M5-T2.
                Debug.LogWarning(
                    $"{nameof(TrackableLifecycleManager)}: '{qr.Payload}' decoded but the QR reported " +
                    "no plane to place it; showing the error state.", this);
                renderer.ShowError(qr);
            }
        }

        // A pipeline continuation may only touch the instance while the entry is still tracked and its
        // renderer alive (the renderer is a UnityEngine.Object, so use the lifetime-aware null check).
        private static bool IsLive(TrackedQrEntry entry) => !entry.IsRetired && entry.Renderer != null;

        private static void ShowErrorIfLive(TrackedQrEntry entry)
        {
            if (IsLive(entry))
            {
                entry.Renderer.ShowError(entry.QrCode);
            }
        }

        // --- Test seam (M5-T3) -----------------------------------------------
        // The manager is the composition root: Awake wires the resolver from a serialized config and
        // OnEnable subscribes to the detection source — neither runs in EditMode (no play mode, no
        // MRUK). These internal hooks let the multi-QR EditMode tests inject a fake resolver + content
        // prefab and raise detect/lost directly, so "N independent pipelines, no cross-QR shared state"
        // (§4) is provable off device (§8) without widening the public API. With a fake resolver that
        // completes synchronously, the whole per-QR pipeline runs before the raise call returns.

        internal void ConfigureForTests(IContentResolver resolver, ContentRenderer prefab)
        {
            _resolver = resolver;
            contentInstancePrefab = prefab;
        }

        internal void RaiseDetectedForTests(IQrCode qrCode) => OnQrCodeDetected(qrCode);

        internal void RaiseLostForTests(IQrCode qrCode) => OnQrCodeLost(qrCode);
    }
}
