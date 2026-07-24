using NUnit.Framework;
using QRReader.Rendering;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="SpinnerRotation"/> (M4-T4): the time→angle mapping is linear, normalized to
    /// [0, 360), and correct for wraparound and a negative (clockwise) speed.
    /// </summary>
    public class SpinnerRotationTests
    {
        [Test]
        public void Starts_At_Zero()
        {
            Assert.That(SpinnerRotation.AngleDegrees(0f, 180f), Is.EqualTo(0f));
        }

        [Test]
        public void Is_Linear_In_Time()
        {
            Assert.That(SpinnerRotation.AngleDegrees(1f, 180f), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(SpinnerRotation.AngleDegrees(0.5f, 180f), Is.EqualTo(90f).Within(1e-4f));
        }

        [Test]
        public void Wraps_Into_Zero_To_360()
        {
            // 3s × 180°/s = 540° → 180°.
            Assert.That(SpinnerRotation.AngleDegrees(3f, 180f), Is.EqualTo(180f).Within(1e-3f));
            // A full turn lands back near 0.
            Assert.That(SpinnerRotation.AngleDegrees(2f, 180f), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Negative_Speed_Normalizes_Into_Range()
        {
            float angle = SpinnerRotation.AngleDegrees(1f, -90f); // -90° → 270°
            Assert.That(angle, Is.EqualTo(270f).Within(1e-3f));
            Assert.That(angle, Is.GreaterThanOrEqualTo(0f).And.LessThan(360f));
        }
    }
}
