using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ErrorIconTextureFactory"/> (M4-T5): it produces a square RGBA disc icon
    /// with transparent corners, a coloured disc, and a light exclamation glyph; clamps tiny sizes; and
    /// hands ownership to the caller.
    /// </summary>
    public class ErrorIconTextureFactoryTests
    {
        private readonly List<Texture2D> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D texture in _created)
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }

            _created.Clear();
        }

        [Test]
        public void Creates_A_Square_Rgba_Texture_Of_Requested_Size()
        {
            Texture2D texture = Track(ErrorIconTextureFactory.Create(64));

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(64));
            Assert.That(texture.height, Is.EqualTo(64));
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
        }

        [Test]
        public void Clamps_Too_Small_A_Size()
        {
            Assert.That(Track(ErrorIconTextureFactory.Create(1)).width, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void Corners_Are_Transparent_And_The_Disc_Is_Opaque()
        {
            const int size = 64;
            Texture2D texture = Track(ErrorIconTextureFactory.Create(size));
            Color32[] pixels = texture.GetPixels32();

            // Outside the disc (a corner) is transparent.
            Assert.That(pixels[0].a, Is.EqualTo(0));

            // The centre sits inside the disc → opaque.
            Color32 centre = pixels[(size / 2) * size + (size / 2)];
            Assert.That(centre.a, Is.GreaterThan(200));
        }

        [Test]
        public void Has_A_Coloured_Disc_And_A_Light_Glyph()
        {
            const int size = 128;
            Texture2D texture = Track(ErrorIconTextureFactory.Create(size));
            Color32[] pixels = texture.GetPixels32();

            // A point in the disc away from the central glyph should be red-dominant (the disc colour).
            int discX = size / 2 - (int)(size * 0.3f);
            Color32 disc = pixels[(size / 2) * size + discX];
            Assert.That(disc.a, Is.GreaterThan(200));
            Assert.That(disc.r, Is.GreaterThan(disc.g));
            Assert.That(disc.r, Is.GreaterThan(disc.b));

            // Somewhere the glyph is near-white (the exclamation mark).
            bool hasLightGlyph = false;
            foreach (Color32 p in pixels)
            {
                if (p.a > 200 && p.r > 230 && p.g > 230 && p.b > 230)
                {
                    hasLightGlyph = true;
                    break;
                }
            }

            Assert.That(hasLightGlyph, Is.True, "expected a near-white exclamation glyph");
        }

        private Texture2D Track(Texture2D texture)
        {
            _created.Add(texture);
            return texture;
        }
    }
}
