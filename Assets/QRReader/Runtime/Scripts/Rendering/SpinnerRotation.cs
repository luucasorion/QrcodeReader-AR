using UnityEngine;

namespace QRReader.Rendering
{
    /// <summary>
    /// Pure rotation math for the loading spinner (M4-T4). Split out from <see cref="LoadingSpinner"/>
    /// so the time→angle mapping stays EditMode-unit-testable off device (plan R1, §8).
    /// </summary>
    public static class SpinnerRotation
    {
        /// <summary>
        /// The spinner's rotation, in degrees, after <paramref name="elapsedSeconds"/> spinning at
        /// <paramref name="degreesPerSecond"/>. Normalized to <c>[0, 360)</c> so it never grows
        /// unbounded, and correct for a negative speed (clockwise) or negative elapsed time.
        /// </summary>
        public static float AngleDegrees(float elapsedSeconds, float degreesPerSecond)
        {
            float angle = (elapsedSeconds * degreesPerSecond) % 360f;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return angle;
        }
    }
}
