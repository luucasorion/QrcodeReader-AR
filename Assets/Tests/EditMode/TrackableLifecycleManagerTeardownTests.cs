using System;
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
    /// Tests for teardown on removal (M5-T4, §7): when a QR is lost the
    /// <see cref="TrackableLifecycleManager"/> destroys that QR's content instance and frees the
    /// textures it owns (image and — the primary memory risk — GIF frames), with no caching and no
    /// effect on any other tracked QR. Drives the pipeline through the internal test seam with a fake,
    /// synchronous <see cref="IContentResolver"/> so it runs off device (§8).
    /// </summary>
    public class TrackableLifecycleManagerTeardownTests
    {
        // A 1×1 transparent GIF (same fixture as GifDecoderTests) — decodes to at least one frame.
        private const string OnePixelGifBase64 =
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

        private readonly List<UnityEngine.Object> _created = new();
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
            foreach (UnityEngine.Object obj in _created)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        [Test]
        public void Removing_A_Qr_Destroys_Its_Content_Instance()
        {
            IQrCode qr = NewQr("https://example.com/a.png");
            _resolver.Map(qr.Payload, Png(4, 4));
            _manager.RaiseDetectedForTests(qr);
            GameObject instance = EntryFor(qr).Renderer.gameObject;
            Assert.That(instance, Is.Not.Null);

            _manager.RaiseLostForTests(qr);

            // Unity's overloaded == reports a destroyed object as null.
            Assert.That(instance == null, Is.True, "content instance should be destroyed on removal");
        }

        [Test]
        public void Removing_A_Qr_Disposes_Its_Decoded_Content_And_Frees_Its_Texture()
        {
            IQrCode qr = NewQr("https://example.com/a.png");
            _resolver.Map(qr.Payload, Png(4, 4));
            _manager.RaiseDetectedForTests(qr);
            DecodedContent content = EntryFor(qr).Content;
            Texture texture = content.Frames[0].Texture;
            Assert.That(content.IsDisposed, Is.False);
            Assert.That(texture, Is.Not.Null);

            _manager.RaiseLostForTests(qr);

            Assert.That(content.IsDisposed, Is.True);
            Assert.That(texture == null, Is.True, "decoded texture should be freed on removal");
        }

        [Test]
        public void Removing_A_Gif_Qr_Frees_Its_Frame_Textures()
        {
            IQrCode qr = NewQr("https://example.com/anim.gif");
            _resolver.Map(qr.Payload, Convert.FromBase64String(OnePixelGifBase64), "image/gif");
            _manager.RaiseDetectedForTests(qr);
            DecodedContent content = EntryFor(qr).Content;
            Assert.That(content, Is.Not.Null, "GIF should decode to a content handle");

            // Capture the owned frame textures before teardown clears the handle.
            var frameTextures = new List<Texture>();
            foreach (GifFrame frame in content.Frames)
            {
                frameTextures.Add(frame.Texture);
            }

            Assert.That(frameTextures, Is.Not.Empty);

            _manager.RaiseLostForTests(qr);

            Assert.That(content.IsDisposed, Is.True);
            foreach (Texture frameTexture in frameTextures)
            {
                Assert.That(frameTexture == null, Is.True, "every GIF frame texture should be freed");
            }
        }

        [Test]
        public void Removing_One_Qr_Does_Not_Tear_Down_Another()
        {
            IQrCode a = NewQr("https://example.com/a.png");
            IQrCode b = NewQr("https://example.com/b.png");
            _resolver.Map(a.Payload, Png(4, 4));
            _resolver.Map(b.Payload, Png(4, 4));
            _manager.RaiseDetectedForTests(a);
            _manager.RaiseDetectedForTests(b);

            TrackedQrEntry entryB = EntryFor(b);
            GameObject instanceB = entryB.Renderer.gameObject;
            DecodedContent contentB = entryB.Content;
            Texture textureB = contentB.Frames[0].Texture;

            _manager.RaiseLostForTests(a);

            // B's instance, content, and texture are all untouched (no shared teardown, §4/§7).
            Assert.That(instanceB == null, Is.False);
            Assert.That(contentB.IsDisposed, Is.False);
            Assert.That(textureB == null, Is.False);
            Assert.That(entryB.Renderer.CurrentState, Is.EqualTo(ContentRenderer.State.Media));
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

        // Returns a completed result per URL, so the whole per-QR pipeline runs synchronously within
        // the raise call — no network, no play mode.
        private sealed class FakeResolver : IContentResolver
        {
            private readonly Dictionary<string, ResolveResult> _byUrl = new();

            public void Map(string url, byte[] bytes, string contentType = "image/png")
            {
                var headers = new Dictionary<string, string> { ["Content-Type"] = contentType };
                _byUrl[url] = ResolveResult.Succeeded(bytes, headers);
            }

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
