using System;
using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Decoding;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="DecodedContent"/> (M3-T4): it models image vs GIF uniformly, and
    /// <see cref="DecodedContent.Dispose"/> releases every owned texture (idempotently) so teardown
    /// frees GPU memory (§7).
    /// </summary>
    public class DecodedContentTests
    {
        private readonly List<Texture2D> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D texture in _created)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ForImage_Is_A_Single_Static_Frame()
        {
            Texture2D texture = NewTexture();

            var content = DecodedContent.ForImage(texture);

            Assert.That(content.FrameCount, Is.EqualTo(1));
            Assert.That(content.IsAnimated, Is.False);
            Assert.That(content.Frames[0].Texture, Is.SameAs(texture));
            Assert.That(content.Frames[0].DelayMs, Is.EqualTo(0));
        }

        [Test]
        public void ForGif_Preserves_Frames_And_Order()
        {
            var frames = new List<GifFrame>
            {
                new GifFrame(NewTexture(), 100),
                new GifFrame(NewTexture(), 40),
                new GifFrame(NewTexture(), 40),
            };

            var content = DecodedContent.ForGif(frames);

            Assert.That(content.FrameCount, Is.EqualTo(3));
            Assert.That(content.IsAnimated, Is.True);
            Assert.That(content.Frames[0].DelayMs, Is.EqualTo(100));
            Assert.That(content.Frames[1].Texture, Is.SameAs(frames[1].Texture));
        }

        [Test]
        public void Dispose_Releases_Every_Owned_Texture()
        {
            Texture2D a = NewTexture();
            Texture2D b = NewTexture();
            var content = DecodedContent.ForGif(new List<GifFrame> { new GifFrame(a, 20), new GifFrame(b, 20) });

            content.Dispose();

            Assert.That(content.IsDisposed, Is.True);
            Assert.That(content.Frames, Is.Empty);
            // Unity's overloaded == reports a destroyed object as null.
            Assert.That(a == null, Is.True, "frame texture a should be destroyed");
            Assert.That(b == null, Is.True, "frame texture b should be destroyed");
        }

        [Test]
        public void Dispose_Is_Idempotent()
        {
            var content = DecodedContent.ForImage(NewTexture());

            content.Dispose();
            Assert.That(() => content.Dispose(), Throws.Nothing);
            Assert.That(content.IsDisposed, Is.True);
        }

        [Test]
        public void Factories_Reject_Invalid_Input()
        {
            Assert.That(() => DecodedContent.ForImage(null), Throws.ArgumentNullException);
            Assert.That(() => DecodedContent.ForGif(null), Throws.ArgumentNullException);
            Assert.That(() => DecodedContent.ForGif(new List<GifFrame>()), Throws.ArgumentException);
        }

        [Test]
        public void ForGif_Copies_The_Frame_List()
        {
            var frames = new List<GifFrame> { new GifFrame(NewTexture(), 30) };
            var content = DecodedContent.ForGif(frames);

            frames.Add(new GifFrame(NewTexture(), 30)); // mutate source after construction

            Assert.That(content.FrameCount, Is.EqualTo(1), "handle must own an independent copy");
        }

        private Texture2D NewTexture()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            _created.Add(texture);
            return texture;
        }
    }
}
