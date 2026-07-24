using System;
using System.Collections.Generic;
using UnityEngine;

namespace QRReader.Decoding
{
    /// <summary>
    /// Owns the texture(s) produced by decoding one QR payload — a single still image or an animated
    /// GIF's frames — so they can be released together on teardown (architecture.md §3.6, §7). GIF
    /// frame textures are the primary memory risk on Quest, so a single handle with a definite owner
    /// and an explicit release is how the lifecycle frees them on <c>TrackableRemoved</c> (M5-T4).
    /// </summary>
    /// <remarks>
    /// A still image is modelled as a one-frame sequence (delay 0), so the renderer can treat both
    /// uniformly (M4-T6): <see cref="IsAnimated"/> is simply "more than one frame". Constructed via the
    /// factories from the M3-T2/T3 decoder outputs; the caller hands ownership over and must not
    /// destroy the textures itself. <see cref="Dispose"/> is idempotent.
    /// </remarks>
    public sealed class DecodedContent : IDisposable
    {
        private readonly List<GifFrame> _frames;

        private DecodedContent(List<GifFrame> frames)
        {
            _frames = frames;
        }

        /// <summary>Takes ownership of a single decoded image texture (<see cref="ImageDecoder"/>).</summary>
        /// <exception cref="ArgumentNullException"><paramref name="texture"/> is null.</exception>
        public static DecodedContent ForImage(Texture2D texture)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            return new DecodedContent(new List<GifFrame> { new GifFrame(texture, 0) });
        }

        /// <summary>Takes ownership of decoded GIF frames (<see cref="GifDecoder"/>), in order.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="frames"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="frames"/> is empty.</exception>
        public static DecodedContent ForGif(IReadOnlyList<GifFrame> frames)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("GIF content requires at least one frame.", nameof(frames));
            }

            // Copy so the handle owns its own list and can't be mutated behind its back.
            return new DecodedContent(new List<GifFrame>(frames));
        }

        /// <summary>The owned frames in playback order; empty once disposed.</summary>
        public IReadOnlyList<GifFrame> Frames => _frames;

        /// <summary>Number of frames (1 for a still image).</summary>
        public int FrameCount => _frames.Count;

        /// <summary>True when there's more than one frame, i.e. the content animates.</summary>
        public bool IsAnimated => _frames.Count > 1;

        /// <summary>True once the owned textures have been released.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Destroys every owned texture and clears the handle. Safe to call more than once.</summary>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            foreach (GifFrame frame in _frames)
            {
                TextureCleanup.Destroy(frame.Texture);
            }

            _frames.Clear();
        }
    }
}
