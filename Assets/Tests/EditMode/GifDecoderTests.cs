using System;
using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Decoding;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="GifDecoder"/> (M3-T3): a valid GIF decodes to caller-owned frame textures,
    /// and malformed/empty bytes fail cleanly without throwing (the no-crash guarantee the M3-T5 error
    /// routing relies on).
    /// </summary>
    public class GifDecoderTests
    {
        // A minimal, valid 1x1 single-frame GIF (the canonical transparent 1x1 GIF89a).
        private const string OnePixelGifBase64 =
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

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
        public void Decodes_Valid_Gif_To_At_Least_One_Frame()
        {
            byte[] gif = Convert.FromBase64String(OnePixelGifBase64);

            bool ok = GifDecoder.TryDecode(gif, out IReadOnlyList<GifFrame> frames);

            Assert.That(ok, Is.True);
            Assert.That(frames, Is.Not.Null.And.Not.Empty);

            GifFrame first = frames[0];
            Assert.That(first.Texture, Is.Not.Null);
            _created.Add(first.Texture);
            Assert.That(first.Texture.width, Is.EqualTo(1));
            Assert.That(first.Texture.height, Is.EqualTo(1));
        }

        [Test]
        public void Garbage_Bytes_Fail_Without_Throwing()
        {
            var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xDE, 0xAD, 0xBE, 0xEF };

            bool ok = false;
            Assert.That(() => ok = GifDecoder.TryDecode(garbage, out _), Throws.Nothing);
            Assert.That(ok, Is.False);
        }

        [Test]
        public void Null_Or_Empty_Bytes_Return_False()
        {
            Assert.That(GifDecoder.TryDecode(null, out IReadOnlyList<GifFrame> fromNull), Is.False);
            Assert.That(fromNull, Is.Null);

            Assert.That(GifDecoder.TryDecode(new byte[0], out IReadOnlyList<GifFrame> fromEmpty), Is.False);
            Assert.That(fromEmpty, Is.Null);
        }
    }
}
