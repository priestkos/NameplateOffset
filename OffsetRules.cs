using System;
using System.Globalization;

namespace NameplateOffset;

/// <summary>Pure offset/baseline rules, kept free of game types so tests/NameplateOffset.Tests can link this file.</summary>
internal static class OffsetRules
{
    public const float Min = -1.5f;
    public const float Max = 3f;

    /// <summary>Clamps a finite offset into range; NaN/Infinity becomes 0.</summary>
    public static float Sanitize(float offset) => float.IsFinite(offset) ? Math.Clamp(offset, Min, Max) : 0f;

    /// <summary>Parses a user-typed offset in yalms; rejects non-numbers and NaN/Infinity.</summary>
    public static bool TryParse(string text, out float offset)
    {
        var ok = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value);
        offset = ok ? Math.Clamp(value, Min, Max) : 0f;
        return ok;
    }

    /// <summary>
    ///     How many frames a changed game value must hold before it is adopted as the new baseline. A value that
    ///     only lasts a frame or two is noise; following it turns into the plate wobbling up and down at frame rate.
    /// </summary>
    public const int StableFrames = 4;

    public const float Epsilon = 0.0001f;

    /// <summary>
    ///     Per-object baseline state. Tracks the value the game itself wanted, what we last wrote, and a candidate
    ///     game value that has not yet held still long enough to be trusted.
    /// </summary>
    internal sealed class PlateState
    {
        public float BaselineY;
        public float WrittenY;
        public bool Adopted;

        private float candidate;
        private int candidateFrames;

        /// <summary>
        ///     Refreshes the baseline from the live game value and returns false when the object must be left alone
        ///     (an untracked object carrying a non-finite value). A finite value seen for the first time is adopted
        ///     immediately; after that, a changed value is adopted only once it has held for <see cref="StableFrames"/>.
        /// </summary>
        public bool TryAdopt(float current)
        {
            if (!float.IsFinite(current))
            {
                // Never adopt garbage; a tracked plate keeps the baseline it already has.
                return this.Adopted;
            }

            if (this.Adopted && MathF.Abs(current - this.WrittenY) < Epsilon)
            {
                this.candidateFrames = 0;
                return true;
            }

            if (this.candidateFrames > 0 && MathF.Abs(current - this.candidate) < Epsilon)
            {
                this.candidateFrames++;
            }
            else
            {
                this.candidate = current;
                this.candidateFrames = 1;
            }

            if (this.Adopted && this.candidateFrames < StableFrames)
            {
                return true;
            }

            this.candidateFrames = 0;
            this.BaselineY = current;
            return true;
        }
    }
}
