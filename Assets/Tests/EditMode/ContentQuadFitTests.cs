using NUnit.Framework;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ContentQuadFit"/> (M4-T2): the quad's local scale is the QR plane size ×
    /// the scale factor, flat in Z, and robust to a mirrored/negative <c>PlaneRect</c>.
    /// </summary>
    public class ContentQuadFitTests
    {
        [Test]
        public void Scales_Plane_Size_By_Factor()
        {
            var plane = new Rect(0f, 0f, 0.1f, 0.2f); // 10cm × 20cm

            Vector3 scale = ContentQuadFit.ComputeLocalScale(plane, 3f);

            Assert.That(scale.x, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(scale.y, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(scale.z, Is.EqualTo(1f));
        }

        [Test]
        public void Factor_Of_One_Is_Exact_Plane_Size()
        {
            var plane = new Rect(0f, 0f, 0.08f, 0.08f);

            Vector3 scale = ContentQuadFit.ComputeLocalScale(plane, 1f);

            Assert.That(scale.x, Is.EqualTo(0.08f).Within(1e-5f));
            Assert.That(scale.y, Is.EqualTo(0.08f).Within(1e-5f));
        }

        [Test]
        public void Negative_Plane_Dimensions_Produce_Positive_Scale()
        {
            var mirrored = new Rect(0f, 0f, -0.1f, -0.2f);

            Vector3 scale = ContentQuadFit.ComputeLocalScale(mirrored, 2f);

            Assert.That(scale.x, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(scale.y, Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void Negative_Factor_Clamps_To_Zero_Not_Inside_Out()
        {
            var plane = new Rect(0f, 0f, 0.1f, 0.1f);

            Vector3 scale = ContentQuadFit.ComputeLocalScale(plane, -5f);

            Assert.That(scale.x, Is.EqualTo(0f));
            Assert.That(scale.y, Is.EqualTo(0f));
            Assert.That(scale.z, Is.EqualTo(1f));
        }
    }
}
