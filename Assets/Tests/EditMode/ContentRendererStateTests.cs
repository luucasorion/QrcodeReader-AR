using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Decoding;
using QRReader.DetectionSource;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for the <see cref="ContentRenderer"/> state API (M4-T6): exactly one of {loading, error,
    /// media} is visible per state, the media surface shows the decoded texture, and transitions swap
    /// cleanly. Verifies the wiring between the renderer and its child loading/error visuals.
    /// </summary>
    public class ContentRendererStateTests
    {
        private readonly List<Object> _created = new();
        private DecodedContent _content;

        [TearDown]
        public void TearDown()
        {
            _content?.Dispose();
            _content = null;

            foreach (Object obj in _created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ShowLoading_Shows_Only_The_Spinner()
        {
            Rig rig = NewRig();

            rig.Renderer.ShowLoading(NewQr());

            Assert.That(rig.Renderer.CurrentState, Is.EqualTo(ContentRenderer.State.Loading));
            Assert.That(rig.Spinner.enabled, Is.True);
            Assert.That(rig.Error.enabled, Is.False);
            Assert.That(rig.Media.enabled, Is.False);
        }

        [Test]
        public void ShowError_Shows_Only_The_Error_Icon()
        {
            Rig rig = NewRig();

            rig.Renderer.ShowError(NewQr());

            Assert.That(rig.Renderer.CurrentState, Is.EqualTo(ContentRenderer.State.Error));
            Assert.That(rig.Error.enabled, Is.True);
            Assert.That(rig.Spinner.enabled, Is.False);
            Assert.That(rig.Media.enabled, Is.False);
        }

        [Test]
        public void ShowMedia_Shows_Only_The_Media_Surface_With_The_Texture()
        {
            Rig rig = NewRig();
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, mipChain: false);
            _created.Add(texture);
            _content = DecodedContent.ForImage(texture);

            bool shown = rig.Renderer.ShowMedia(NewQr(), _content);

            Assert.That(shown, Is.True);
            Assert.That(rig.Renderer.CurrentState, Is.EqualTo(ContentRenderer.State.Media));
            Assert.That(rig.Media.enabled, Is.True);
            Assert.That(rig.Spinner.enabled, Is.False);
            Assert.That(rig.Error.enabled, Is.False);
            Assert.That(rig.Renderer.DisplayedTexture, Is.SameAs(texture));
        }

        [Test]
        public void ShowMedia_Declines_Empty_Content()
        {
            Rig rig = NewRig();

            bool shown = rig.Renderer.ShowMedia(NewQr(), null);

            Assert.That(shown, Is.False);
            Assert.That(rig.Renderer.CurrentState, Is.Not.EqualTo(ContentRenderer.State.Media));
        }

        [Test]
        public void Transition_Loading_To_Media_To_Error_Swaps_Visuals()
        {
            Rig rig = NewRig();
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            _created.Add(texture);
            _content = DecodedContent.ForImage(texture);
            IQrCode qr = NewQr();

            rig.Renderer.ShowLoading(qr);
            rig.Renderer.ShowMedia(qr, _content);
            Assert.That(rig.Media.enabled, Is.True);
            Assert.That(rig.Spinner.enabled, Is.False);

            rig.Renderer.ShowError(qr);
            Assert.That(rig.Error.enabled, Is.True);
            Assert.That(rig.Media.enabled, Is.False);
            Assert.That(rig.Spinner.enabled, Is.False);
        }

        private Rig NewRig()
        {
            var root = new GameObject("content");
            _created.Add(root);
            var renderer = root.AddComponent<ContentRenderer>(); // RequireComponent adds the MeshRenderer

            var spinnerGo = new GameObject("spinner");
            spinnerGo.transform.SetParent(root.transform);
            var spinner = spinnerGo.AddComponent<LoadingSpinner>();

            var errorGo = new GameObject("error");
            errorGo.transform.SetParent(root.transform);
            var error = errorGo.AddComponent<ErrorIcon>();

            return new Rig
            {
                Renderer = renderer,
                Media = root.GetComponent<MeshRenderer>(),
                Spinner = spinnerGo.GetComponent<MeshRenderer>(),
                Error = errorGo.GetComponent<MeshRenderer>(),
            };
        }

        private IQrCode NewQr()
        {
            var poseGo = new GameObject("pose");
            _created.Add(poseGo);
            return new FakeQrCode { PoseTransform = poseGo.transform, Plane = new Rect(0f, 0f, 0.1f, 0.1f) };
        }

        private struct Rig
        {
            public ContentRenderer Renderer;
            public MeshRenderer Media;
            public MeshRenderer Spinner;
            public MeshRenderer Error;
        }

        private sealed class FakeQrCode : IQrCode
        {
            public string Payload => "https://example.com/a.png";
            public Transform PoseTransform;
            public Rect? Plane;
            public Transform Pose => PoseTransform;
            public Rect? PlaneRect => Plane;
            public bool IsTracked => true;
        }
    }
}
