using System;
using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Classification;
using QRReader.Decoding;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="MediaDecoder"/> (M3-T5): decodable image/GIF bytes yield an owned
    /// <see cref="DecodedContent"/> handle, while unsupported kinds and malformed/empty bytes fail to a
    /// typed <see cref="DecodeFailure"/> without throwing — the no-crash routing to the error state.
    /// </summary>
    public class MediaDecoderTests
    {
        // A minimal, valid 1x1 single-frame GIF (the canonical transparent 1x1 GIF89a), matching
        // GifDecoderTests so the GIF path is exercised with real, decodable bytes.
        private const string OnePixelGifBase64 =
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in _created)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        [Test]
        public void Image_Kind_Decodes_To_A_Single_Frame_Handle()
        {
            byte[] png = MakePng(6, 4);

            DecodeResult result = MediaDecoder.Decode(ContentKind.Image, png);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Failure, Is.EqualTo(DecodeFailure.None));
            Assert.That(result.Content, Is.Not.Null);
            Track(result.Content);
            Assert.That(result.Content.FrameCount, Is.EqualTo(1));
            Assert.That(result.Content.IsAnimated, Is.False);
            Assert.That(result.Content.Frames[0].Texture.width, Is.EqualTo(6));
        }

        [Test]
        public void Gif_Kind_Decodes_To_Frames()
        {
            byte[] gif = Convert.FromBase64String(OnePixelGifBase64);

            DecodeResult result = MediaDecoder.Decode(ContentKind.Gif, gif);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Content, Is.Not.Null);
            Track(result.Content);
            Assert.That(result.Content.FrameCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Unsupported_Kind_Fails_Without_Touching_Bytes()
        {
            // Real bytes, but an unsupported classification: no decode should be attempted.
            DecodeResult result = MediaDecoder.Decode(ContentKind.Unsupported, MakePng(2, 2));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(DecodeFailure.Unsupported));
            Assert.That(result.Content, Is.Null);
        }

        [Test]
        public void Malformed_Image_Bytes_Fail_As_Undecodable_Without_Throwing()
        {
            var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xDE, 0xAD, 0xBE, 0xEF };

            DecodeResult result = default;
            Assert.That(() => result = MediaDecoder.Decode(ContentKind.Image, garbage), Throws.Nothing);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(DecodeFailure.Undecodable));
            Assert.That(result.Content, Is.Null);
        }

        [Test]
        public void Malformed_Gif_Bytes_Fail_As_Undecodable_Without_Throwing()
        {
            var garbage = new byte[] { (byte)'G', (byte)'I', (byte)'F', 0x00, 0x11, 0x22 };

            DecodeResult result = default;
            Assert.That(() => result = MediaDecoder.Decode(ContentKind.Gif, garbage), Throws.Nothing);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(DecodeFailure.Undecodable));
        }

        [Test]
        public void Null_Or_Empty_Bytes_Fail_As_Undecodable()
        {
            Assert.That(MediaDecoder.Decode(ContentKind.Image, null).Failure,
                Is.EqualTo(DecodeFailure.Undecodable));
            Assert.That(MediaDecoder.Decode(ContentKind.Gif, new byte[0]).Failure,
                Is.EqualTo(DecodeFailure.Undecodable));
        }

        private void Track(DecodedContent content)
        {
            foreach (GifFrame frame in content.Frames)
            {
                _created.Add(frame.Texture);
            }
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
