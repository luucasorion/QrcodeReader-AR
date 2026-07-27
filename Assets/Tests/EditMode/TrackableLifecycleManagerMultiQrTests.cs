using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using QRReader.Decoding;
using QRReader.DetectionSource;
using QRReader.Lifecycle;
using QRReader.Rendering;
using QRReader.Resolver;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for N simultaneous QRs (M5-T3): the <see cref="TrackableLifecycleManager"/> runs one
    /// independent pipeline + content instance per QR with no cross-QR shared state — concurrent QRs
    /// resolve independently (one to media, one to error), each owns its own instance and texture, and
    /// removing one leaves the others untouched. Drives the pipeline through the internal test seam with
    /// a fake, synchronous <see cref="IContentResolver"/> so it runs off device (§8).
    /// </summary>
    public class TrackableLifecycleManagerMultiQrTests
    {
        private readonly List<Object> _created = new();
        private readonly List<DecodedContent> _decoded = new();
        private TrackableLifecycleManager _manager;
        private FakeResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            var managerGo = new GameObject("manager");
            _created.Add(managerGo);
            _manager = managerGo.AddComponent<TrackableLifecycleManager>();
            _resolver = new FakeResolver();
            _manager.ConfigureForTests(_resolver, NewContentPrefab());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();

            foreach (DecodedContent content in _decoded)
            {
                content?.Dispose();
            }

            _decoded.Clear();
        }

        [Test]
        public void Each_Qr_Gets_Its_Own_Content_Instance()
        {
            IQrCode a = NewQr("https://example.com/a.png");
            IQrCode b = NewQr("https://example.com/b.png");
            _resolver.Map(a.Payload, Png(4, 4));
            _resolver.Map(b.Payload, Png(4, 4));

            Detect(a);
            Detect(b);

            Assert.That(_manager.Count, Is.EqualTo(2));
            TrackedQrEntry entryA = EntryFor(a);
            TrackedQrEntry entryB = EntryFor(b);
            Assert.That(entryA.Renderer, Is.Not.Null);
            Assert.That(entryB.Renderer, Is.Not.Null);
            // Distinct instances — no shared content object across QRs.
            Assert.That(entryA.Renderer, Is.Not.SameAs(entryB.Renderer));
        }

        [Test]
        public void Concurrent_Qrs_Resolve_Independently_To_Media_And_Error()
        {
            IQrCode ok = NewQr("https://example.com/ok.png");
            IQrCode bad = NewQr("https://example.com/bad.png");
            _resolver.Map(ok.Payload, Png(8, 8));
            _resolver.Fail(bad.Payload, ResolveFailure.NetworkError);

            Detect(ok);
            Detect(bad);

            // One QR's failure does not affect the other's success (§4).
            Assert.That(EntryFor(ok).Renderer.CurrentState, Is.EqualTo(ContentRenderer.State.Media));
            Assert.That(EntryFor(bad).Renderer.CurrentState, Is.EqualTo(ContentRenderer.State.Error));
        }

        [Test]
        public void Media_Instances_Own_Distinct_Textures()
        {
            IQrCode a = NewQr("https://example.com/a.png");
            IQrCode b = NewQr("https://example.com/b.png");
            _resolver.Map(a.Payload, Png(4, 4));
            _resolver.Map(b.Payload, Png(6, 6));

            Detect(a);
            Detect(b);

            TrackedQrEntry entryA = EntryFor(a);
            TrackedQrEntry entryB = EntryFor(b);
            TrackDecoded(entryA.Content);
            TrackDecoded(entryB.Content);

            Assert.That(entryA.Content, Is.Not.Null);
            Assert.That(entryB.Content, Is.Not.Null);
            // No shared decoded state: each pipeline produced its own texture on its own surface.
            Assert.That(entryA.Content, Is.Not.SameAs(entryB.Content));
            Assert.That(entryA.Renderer.DisplayedTexture,
                Is.Not.SameAs(entryB.Renderer.DisplayedTexture));
        }

        [Test]
        public void Removing_One_Qr_Leaves_The_Other_Tracked_And_Unaffected()
        {
            IQrCode a = NewQr("https://example.com/a.png");
            IQrCode b = NewQr("https://example.com/b.png");
            _resolver.Map(a.Payload, Png(4, 4));
            _resolver.Map(b.Payload, Png(4, 4));
            Detect(a);
            Detect(b);
            TrackedQrEntry entryB = EntryFor(b);

            _manager.RaiseLostForTests(a);

            Assert.That(_manager.Count, Is.EqualTo(1));
            Assert.That(EntryFor(a), Is.Null, "removed QR should no longer be tracked");
            // The surviving QR keeps its entry, its live instance, and its media state.
            Assert.That(EntryFor(b), Is.SameAs(entryB));
            Assert.That(entryB.IsRetired, Is.False);
            Assert.That(entryB.Renderer, Is.Not.Null);
            Assert.That(entryB.Renderer.CurrentState, Is.EqualTo(ContentRenderer.State.Media));
        }

        // Raise a detect and record the instantiated content clone so TearDown destroys it (the manager
        // doesn't destroy instances until M5-T4).
        private void Detect(IQrCode qr)
        {
            _manager.RaiseDetectedForTests(qr);
            ContentRenderer renderer = EntryFor(qr)?.Renderer;
            if (renderer != null)
            {
                _created.Add(renderer.gameObject);
            }
        }

        private TrackedQrEntry EntryFor(IQrCode qr)
        {
            foreach (TrackedQrEntry entry in _manager.Entries)
            {
                if (entry.QrCode == qr)
                {
                    return entry;
                }
            }

            return null;
        }

        private void TrackDecoded(DecodedContent content)
        {
            if (content != null && !_decoded.Contains(content))
            {
                _decoded.Add(content);
            }
        }

        // A content prefab: a ContentRenderer with child loading/error visuals, cloned per QR.
        private ContentRenderer NewContentPrefab()
        {
            var root = new GameObject("contentPrefab");
            _created.Add(root);
            var renderer = root.AddComponent<ContentRenderer>(); // RequireComponent adds the MeshRenderer

            var spinnerGo = new GameObject("spinner");
            spinnerGo.transform.SetParent(root.transform);
            spinnerGo.AddComponent<LoadingSpinner>();

            var errorGo = new GameObject("error");
            errorGo.transform.SetParent(root.transform);
            errorGo.AddComponent<ErrorIcon>();

            return renderer;
        }

        private FakeQrCode NewQr(string payload)
        {
            var poseGo = new GameObject($"pose ({payload})");
            _created.Add(poseGo);
            return new FakeQrCode
            {
                PayloadValue = payload,
                PoseTransform = poseGo.transform,
                Plane = new Rect(0f, 0f, 0.1f, 0.1f),
            };
        }

        private byte[] Png(int width, int height)
        {
            var source = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            _created.Add(source);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(20, 140, 220, 255);
            }

            source.SetPixels32(pixels);
            source.Apply();
            return source.EncodeToPNG();
        }

        // A resolver that returns a completed result per URL, so the whole per-QR pipeline runs
        // synchronously within the raise call — no network, no play mode.
        private sealed class FakeResolver : IContentResolver
        {
            private readonly Dictionary<string, ResolveResult> _byUrl = new();

            public void Map(string url, byte[] bytes)
            {
                var headers = new Dictionary<string, string> { ["Content-Type"] = "image/png" };
                _byUrl[url] = ResolveResult.Succeeded(bytes, headers);
            }

            public void Fail(string url, ResolveFailure reason) =>
                _byUrl[url] = ResolveResult.Failed(reason);

            public Task<ResolveResult> GetAsync(string url)
            {
                ResolveResult result = _byUrl.TryGetValue(url, out ResolveResult mapped)
                    ? mapped
                    : ResolveResult.Failed(ResolveFailure.NetworkError);
                return Task.FromResult(result);
            }
        }

        private sealed class FakeQrCode : IQrCode
        {
            public string PayloadValue;
            public Transform PoseTransform;
            public Rect? Plane;
            public string Payload => PayloadValue;
            public Transform Pose => PoseTransform;
            public Rect? PlaneRect => Plane;
            public bool IsTracked => true;
        }
    }
}
