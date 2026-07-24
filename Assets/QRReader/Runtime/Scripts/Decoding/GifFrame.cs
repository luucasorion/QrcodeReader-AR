using UnityEngine;

namespace QRReader.Decoding
{
    /// <summary>
    /// One decoded GIF frame (M3-T3): the frame's <see cref="Texture"/> and how long to display it
    /// before the next frame (<see cref="DelayMs"/>). The texture is <b>owned by the caller</b> and
    /// must be destroyed on teardown (architecture.md §3.6, §7) — the M3-T4 handle takes that ownership
    /// for all frames.
    /// </summary>
    public readonly struct GifFrame
    {
        public GifFrame(Texture2D texture, int delayMs)
        {
            Texture = texture;
            DelayMs = delayMs;
        }

        /// <summary>The frame image as an independent texture the caller owns.</summary>
        public Texture2D Texture { get; }

        /// <summary>How long to show this frame, in milliseconds, as authored in the GIF.</summary>
        public int DelayMs { get; }
    }
}
