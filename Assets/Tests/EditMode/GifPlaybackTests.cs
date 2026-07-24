using NUnit.Framework;
using QRReader.Rendering;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="GifPlayback"/> (M4-T6): the frame index at a given elapsed time loops over
    /// the summed delays, treats a single/empty/all-zero clip as static, and skips zero-length frames.
    /// </summary>
    public class GifPlaybackTests
    {
        [Test]
        public void Single_Frame_Is_Static()
        {
            Assert.That(GifPlayback.FrameIndexAt(1234f, new[] { 100 }), Is.EqualTo(0));
        }

        [Test]
        public void Null_Or_Empty_Is_Static()
        {
            Assert.That(GifPlayback.FrameIndexAt(50f, null), Is.EqualTo(0));
            Assert.That(GifPlayback.FrameIndexAt(50f, new int[0]), Is.EqualTo(0));
        }

        [Test]
        public void Advances_Then_Loops_Over_Total_Duration()
        {
            var delays = new[] { 100, 100 }; // total 200ms

            Assert.That(GifPlayback.FrameIndexAt(0f, delays), Is.EqualTo(0));
            Assert.That(GifPlayback.FrameIndexAt(50f, delays), Is.EqualTo(0));
            Assert.That(GifPlayback.FrameIndexAt(100f, delays), Is.EqualTo(1));
            Assert.That(GifPlayback.FrameIndexAt(150f, delays), Is.EqualTo(1));
            Assert.That(GifPlayback.FrameIndexAt(200f, delays), Is.EqualTo(0)); // wrapped
            Assert.That(GifPlayback.FrameIndexAt(250f, delays), Is.EqualTo(0));
        }

        [Test]
        public void Uneven_Delays_Map_To_The_Right_Frame()
        {
            var delays = new[] { 40, 200, 40 }; // boundaries at 40, 240, 280

            Assert.That(GifPlayback.FrameIndexAt(10f, delays), Is.EqualTo(0));
            Assert.That(GifPlayback.FrameIndexAt(60f, delays), Is.EqualTo(1));
            Assert.That(GifPlayback.FrameIndexAt(250f, delays), Is.EqualTo(2));
        }

        [Test]
        public void Zero_Length_Frame_Is_Skipped()
        {
            var delays = new[] { 0, 100 }; // frame 0 occupies no time

            Assert.That(GifPlayback.FrameIndexAt(0f, delays), Is.EqualTo(1));
            Assert.That(GifPlayback.FrameIndexAt(99f, delays), Is.EqualTo(1));
        }

        [Test]
        public void All_Zero_Delays_Are_Static()
        {
            Assert.That(GifPlayback.FrameIndexAt(500f, new[] { 0, 0, 0 }), Is.EqualTo(0));
        }

        [Test]
        public void Negative_Elapsed_Wraps_Into_Range()
        {
            var delays = new[] { 100, 100 };
            Assert.That(GifPlayback.FrameIndexAt(-50f, delays), Is.EqualTo(1)); // -50 → 150
        }
    }
}
