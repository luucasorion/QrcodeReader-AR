using System.Collections.Generic;
using NUnit.Framework;
using QRReader.DetectionSource;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ContentRenderer"/> (M4-T2): fitting places the quad at the given pose and
    /// sizes it from the QR's <c>PlaneRect</c> × scale factor, uses the default factor when no config
    /// is assigned, and declines to size a QR with no reported plane yet.
    /// </summary>
    public class ContentRendererTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
        }

        [Test]
        public void Fit_Places_At_Pose_And_Sizes_By_Explicit_Factor()
        {
            ContentRenderer renderer = NewRenderer();
            var position = new Vector3(1f, 2f, 3f);
            Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);

            renderer.Fit(position, rotation, new Rect(0f, 0f, 0.1f, 0.05f), 4f);

            Assert.That(renderer.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(renderer.transform.rotation, rotation), Is.LessThan(1e-3f));
            Assert.That(renderer.transform.localScale.x, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(renderer.transform.localScale.y, Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void ScaleFactor_Defaults_When_No_Config_Assigned()
        {
            ContentRenderer renderer = NewRenderer();

            Assert.That(renderer.ScaleFactor,
                Is.EqualTo(QRReader.Configuration.ContentRendererConfig.DefaultRenderScaleFactor));
        }

        [Test]
        public void Fit_From_QrCode_Uses_Live_Pose_And_Plane()
        {
            ContentRenderer renderer = NewRenderer();
            GameObject poseGo = NewGameObject("pose");
            poseGo.transform.position = new Vector3(5f, 0f, 0f);
            var qr = new FakeQrCode
            {
                PoseTransform = poseGo.transform,
                Plane = new Rect(0f, 0f, 0.1f, 0.1f),
            };

            bool fitted = renderer.Fit(qr);

            Assert.That(fitted, Is.True);
            Assert.That(renderer.transform.position, Is.EqualTo(new Vector3(5f, 0f, 0f)));
            // Default factor (3) × 0.1 = 0.3.
            Assert.That(renderer.transform.localScale.x, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void Fit_Declines_When_No_Plane_Reported_Yet()
        {
            ContentRenderer renderer = NewRenderer();
            GameObject poseGo = NewGameObject("pose");
            var qr = new FakeQrCode { PoseTransform = poseGo.transform, Plane = null };

            bool fitted = renderer.Fit(qr);

            Assert.That(fitted, Is.False);
            // Left unsized rather than collapsed to zero.
            Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one));
        }

        private ContentRenderer NewRenderer() => NewGameObject("quad").AddComponent<ContentRenderer>();

        private GameObject NewGameObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
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
