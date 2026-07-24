using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="SpinnerTextureFactory"/> (M4-T4): it produces a square RGBA texture with a
    /// transparent centre and a visible ring, clamps tiny sizes, and hands ownership to the caller.
    /// </summary>
    public class SpinnerTextureFactoryTests
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
            Texture2D texture = Track(SpinnerTextureFactory.Create(64));

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(64));
            Assert.That(texture.height, Is.EqualTo(64));
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
        }

        [Test]
        public void Clamps_Too_Small_A_Size()
        {
            Texture2D texture = Track(SpinnerTextureFactory.Create(1));

            Assert.That(texture.width, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void Centre_Is_Transparent_And_The_Ring_Has_Opaque_Pixels()
        {
            const int size = 64;
            Texture2D texture = Track(SpinnerTextureFactory.Create(size));
            Color32[] pixels = texture.GetPixels32();

            // The hole in the middle of the ring is transparent.
            Color32 centre = pixels[(size / 2) * size + (size / 2)];
            Assert.That(centre.a, Is.EqualTo(0));

            // Somewhere on the ring the alpha is substantial (the spinner "head").
            byte maxAlpha = 0;
            foreach (Color32 p in pixels)
            {
                if (p.a > maxAlpha)
                {
                    maxAlpha = p.a;
                }
            }

            Assert.That(maxAlpha, Is.GreaterThan(200));
        }

        [Test]
        public void Tint_Colours_The_Ring()
        {
            const int size = 64;
            Texture2D texture = Track(SpinnerTextureFactory.Create(size, Color.red));
            Color32[] pixels = texture.GetPixels32();

            // The most opaque pixel should be red-tinted.
            Color32 head = default;
            foreach (Color32 p in pixels)
            {
                if (p.a > head.a)
                {
                    head = p;
                }
            }

            Assert.That(head.r, Is.GreaterThan(head.g));
            Assert.That(head.r, Is.GreaterThan(head.b));
        }

        private Texture2D Track(Texture2D texture)
        {
            _created.Add(texture);
            return texture;
        }
    }
}
