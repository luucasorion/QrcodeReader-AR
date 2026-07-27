using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Decoding;
using QRReader.DetectionSource;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ContentRenderer.FollowPose"/> (M5-T2): while the QR is tracked the quad
    /// re-aligns to the trackable's live pose each step, preserving the active state's sizing; when the
    /// QR is not tracked (or hidden) it holds its last pose. Drives one alignment step directly rather
    /// than relying on <c>Update</c>, which doesn't run in EditMode.
    /// </summary>
    public class ContentRendererPoseFollowTests
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
        public void FollowPose_Reapplies_The_Live_Pose_While_Tracked()
        {
            ContentRenderer renderer = NewRenderer();
            FakeQrCode qr = NewQr();
            renderer.ShowLoading(qr);

            // Trackable moves after the state was first shown (MRUK's low-frequency pose update).
            qr.PoseTransform.position = new Vector3(7f, 8f, 9f);
            qr.PoseTransform.rotation = Quaternion.Euler(0f, 90f, 0f);

            renderer.FollowPose();

            Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(7f, 8f, 9f)));
            Assert.That(Quaternion.Angle(renderer.transform.rotation, qr.PoseTransform.rotation),
                Is.LessThan(1e-3f));
        }

        [Test]
        public void FollowPose_Holds_Last_Pose_When_Not_Tracked()
        {
            ContentRenderer renderer = NewRenderer();
            FakeQrCode qr = NewQr();
            renderer.ShowError(qr);
            Vector3 placed = renderer.transform.position;

            // Tracking lost, then the (stale) pose moves — the quad must not follow.
            qr.Tracked = false;
            qr.PoseTransform.position = new Vector3(7f, 8f, 9f);

            renderer.FollowPose();

            Assert.That(renderer.transform.position, Is.EqualTo(placed));
        }

        [Test]
        public void FollowPose_Media_Follows_Pose_And_Keeps_The_Letterbox()
        {
            ContentRenderer renderer = NewRenderer();
            FakeQrCode qr = NewQr();
            var texture = new Texture2D(4, 1, TextureFormat.RGBA32, mipChain: false); // 4:1, letterboxed
            _created.Add(texture);
            _content = DecodedContent.ForImage(texture);
            Assert.That(renderer.ShowMedia(qr, _content), Is.True);
            Vector3 scaleAfterShow = renderer.transform.localScale;

            qr.PoseTransform.position = new Vector3(2f, 0f, 5f);
            renderer.FollowPose();

            Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(2f, 0f, 5f)));
            // Aspect-preserving letterbox is recomputed identically — pose follow must not distort it.
            Assert.That(renderer.transform.localScale, Is.EqualTo(scaleAfterShow));
        }

        [Test]
        public void FollowPose_Is_A_NoOp_When_Hidden()
        {
            ContentRenderer renderer = NewRenderer();
            FakeQrCode qr = NewQr();
            renderer.ShowLoading(qr);
            renderer.Hide();

            qr.PoseTransform.position = new Vector3(7f, 8f, 9f);
            renderer.FollowPose();

            Assert.That(renderer.transform.position, Is.Not.EqualTo(new Vector3(7f, 8f, 9f)));
        }

        private ContentRenderer NewRenderer()
        {
            var go = new GameObject("quad");
            _created.Add(go);
            return go.AddComponent<ContentRenderer>(); // RequireComponent adds the MeshRenderer
        }

        private FakeQrCode NewQr()
        {
            var poseGo = new GameObject("pose");
            _created.Add(poseGo);
            return new FakeQrCode { PoseTransform = poseGo.transform, Plane = new Rect(0f, 0f, 0.1f, 0.1f) };
        }

        private sealed class FakeQrCode : IQrCode
        {
            public string Payload => "https://example.com/a.png";
            public Transform PoseTransform;
            public Rect? Plane;
            public bool Tracked = true;
            public Transform Pose => PoseTransform;
            public Rect? PlaneRect => Plane;
            public bool IsTracked => Tracked;
        }
    }
}
