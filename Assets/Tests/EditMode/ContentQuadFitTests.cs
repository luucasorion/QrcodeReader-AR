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

        // --- Letterboxing (M4-T3) --------------------------------------------

        [Test]
        public void Wide_Content_Is_Letterboxed_Within_A_Square_Box()
        {
            // Square 0.3×0.3 box, 2:1 content → full width, half height, centred bars top/bottom.
            var plane = new Rect(0f, 0f, 0.1f, 0.1f);

            Vector3 scale = ContentQuadFit.ComputeLetterboxedLocalScale(plane, 3f, 200, 100);

            Assert.That(scale.x, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(scale.y, Is.EqualTo(0.15f).Within(1e-5f));
        }

        [Test]
        public void Tall_Content_Is_Pillarboxed_Within_A_Square_Box()
        {
            // Square 0.3×0.3 box, 1:2 content → full height, half width.
            var plane = new Rect(0f, 0f, 0.1f, 0.1f);

            Vector3 scale = ContentQuadFit.ComputeLetterboxedLocalScale(plane, 3f, 100, 200);

            Assert.That(scale.x, Is.EqualTo(0.15f).Within(1e-5f));
            Assert.That(scale.y, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void Matching_Aspect_Fills_The_Box()
        {
            var plane = new Rect(0f, 0f, 0.2f, 0.1f); // box aspect 2:1

            Vector3 scale = ContentQuadFit.ComputeLetterboxedLocalScale(plane, 1f, 400, 200); // content 2:1

            Assert.That(scale.x, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(scale.y, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void Fitted_Content_Never_Exceeds_The_Box()
        {
            var plane = new Rect(0f, 0f, 0.12f, 0.08f);

            Vector3 box = ContentQuadFit.ComputeLocalScale(plane, 2f);
            Vector3 wide = ContentQuadFit.ComputeLetterboxedLocalScale(plane, 2f, 500, 100);
            Vector3 tall = ContentQuadFit.ComputeLetterboxedLocalScale(plane, 2f, 100, 500);

            Assert.That(wide.x, Is.LessThanOrEqualTo(box.x + 1e-5f));
            Assert.That(wide.y, Is.LessThanOrEqualTo(box.y + 1e-5f));
            Assert.That(tall.x, Is.LessThanOrEqualTo(box.x + 1e-5f));
            Assert.That(tall.y, Is.LessThanOrEqualTo(box.y + 1e-5f));
        }

        [Test]
        public void Degenerate_Content_Dimensions_Fill_The_Box()
        {
            var plane = new Rect(0f, 0f, 0.1f, 0.1f);
            Vector3 box = ContentQuadFit.ComputeLocalScale(plane, 3f);

            Assert.That(ContentQuadFit.ComputeLetterboxedLocalScale(plane, 3f, 0, 100), Is.EqualTo(box));
            Assert.That(ContentQuadFit.ComputeLetterboxedLocalScale(plane, 3f, 100, 0), Is.EqualTo(box));
        }
    }
}
