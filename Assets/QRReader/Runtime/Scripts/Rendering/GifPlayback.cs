using System.Collections.Generic;
using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// Pure GIF frame-timing math for the media state (M4-T6): which frame is showing after some
    /// elapsed time, given each frame's authored delay. Split out from <see cref="ContentRenderer"/> so
    /// the (fiddly) looping/accumulation logic stays EditMode-unit-testable off device (plan R1, §8).
    /// </summary>
    public static class GifPlayback
    {
        /// <summary>
        /// The 0-based index of the frame visible at <paramref name="elapsedMs"/> for a clip whose
        /// frames have the given <paramref name="delaysMs"/>, looping over the total duration. A single
        /// frame (or null/empty delays, or all-zero delays) is static and always returns 0. Non-positive
        /// per-frame delays contribute no time (a zero-length frame is skipped) rather than stalling.
        /// </summary>
        public static int FrameIndexAt(float elapsedMs, IReadOnlyList<int> delaysMs)
        {
            if (delaysMs == null || delaysMs.Count <= 1)
            {
                return 0;
            }

            float total = 0f;
            for (int i = 0; i < delaysMs.Count; i++)
            {
                total += Mathf.Max(0, delaysMs[i]);
            }

            if (total <= 0f)
            {
                return 0;
            }

            // Wrap into [0, total) for looping playback, correct for negative elapsed time.
            float t = elapsedMs % total;
            if (t < 0f)
            {
                t += total;
            }

            float accumulated = 0f;
            for (int i = 0; i < delaysMs.Count; i++)
            {
                accumulated += Mathf.Max(0, delaysMs[i]);
                if (t < accumulated)
                {
                    return i;
                }
            }

            return delaysMs.Count - 1;
        }
    }
}
