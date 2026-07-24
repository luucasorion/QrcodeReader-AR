using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Decoding;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ImageDecoder"/> (M3-T2): valid bytes decode to a caller-owned texture of
    /// the right size, and malformed/empty bytes fail cleanly without throwing (the no-crash guarantee
    /// the M3-T5 error routing relies on).
    /// </summary>
    public class ImageDecoderTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
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
        public void Decodes_Valid_Png_To_Texture_Of_Expected_Size()
        {
            byte[] png = MakePng(7, 5);

            bool ok = ImageDecoder.TryDecode(png, out Texture2D texture);

            Assert.That(ok, Is.True);
            Assert.That(texture, Is.Not.Null);
            _created.Add(texture);
            Assert.That(texture.width, Is.EqualTo(7));
            Assert.That(texture.height, Is.EqualTo(5));
        }

        [Test]
        public void Garbage_Bytes_Fail_Without_Throwing()
        {
            var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xDE, 0xAD, 0xBE, 0xEF };

            bool ok = false;
            Assert.That(() => ok = ImageDecoder.TryDecode(garbage, out _), Throws.Nothing);
            Assert.That(ok, Is.False);
        }

        [Test]
        public void Null_Or_Empty_Bytes_Return_False()
        {
            Assert.That(ImageDecoder.TryDecode(null, out Texture2D fromNull), Is.False);
            Assert.That(fromNull, Is.Null);

            Assert.That(ImageDecoder.TryDecode(new byte[0], out Texture2D fromEmpty), Is.False);
            Assert.That(fromEmpty, Is.Null);
        }

        // Encodes a solid-colour texture to PNG so the decode test has real, valid image bytes.
        private byte[] MakePng(int width, int height)
        {
            var source = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            _created.Add(source);

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(10, 120, 240, 255);
            }

            source.SetPixels32(pixels);
            source.Apply();
            return source.EncodeToPNG();
        }
    }
}
