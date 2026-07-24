using NUnit.Framework;
using QRReader.Resolver;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="DownloadSizeGuard"/> (M2-T4 cap accounting, M2-T7): the memory guard that
    /// keeps an untrusted server from pushing more than the configured bytes into device memory
    /// (architecture.md §7). Exercised directly so the "over-cap" paths are covered off device without
    /// a live transfer.
    /// </summary>
    public class DownloadSizeGuardTests
    {
        [Test]
        public void Accepts_Chunks_Up_To_The_Cap()
        {
            var guard = new DownloadSizeGuard(100);

            Assert.That(guard.Account(40), Is.True);
            Assert.That(guard.Account(40), Is.True);
            Assert.That(guard.Account(20), Is.True); // total == cap, still allowed
            Assert.That(guard.Exceeded, Is.False);
        }

        [Test]
        public void Trips_When_Streamed_Total_Would_Exceed_The_Cap()
        {
            var guard = new DownloadSizeGuard(100);

            Assert.That(guard.Account(80), Is.True);
            Assert.That(guard.Account(21), Is.False); // 101 > 100 → over
            Assert.That(guard.Exceeded, Is.True);
        }

        [Test]
        public void Stays_Tripped_After_Exceeding()
        {
            var guard = new DownloadSizeGuard(10);

            Assert.That(guard.Account(11), Is.False);
            Assert.That(guard.Exceeded, Is.True);
            Assert.That(guard.Account(1), Is.False); // no further bytes accepted
            Assert.That(guard.Exceeded, Is.True);
        }

        [Test]
        public void Rejects_An_Oversized_Declared_ContentLength_Up_Front()
        {
            var guard = new DownloadSizeGuard(100);

            Assert.That(guard.DeclareContentLength(101), Is.False);
            Assert.That(guard.Exceeded, Is.True);
        }

        [TestCase(0UL)]
        [TestCase(50UL)]
        [TestCase(100UL)] // equal to the cap is fine
        public void Accepts_A_Declared_ContentLength_Within_The_Cap(ulong contentLength)
        {
            var guard = new DownloadSizeGuard(100);

            Assert.That(guard.DeclareContentLength(contentLength), Is.True);
            Assert.That(guard.Exceeded, Is.False);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Non_Positive_Chunk_Is_A_Noop(int chunkLength)
        {
            var guard = new DownloadSizeGuard(100);

            Assert.That(guard.Account(chunkLength), Is.True);
            Assert.That(guard.Exceeded, Is.False);
            Assert.That(guard.Account(100), Is.True); // full cap still available
        }
    }
}
