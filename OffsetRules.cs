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
    ///     Picks the baseline: keep the tracked one if the game value is what we wrote (or is garbage),
    ///     otherwise adopt the game's value. Never adopts a non-finite value; returns false to skip the object.
    /// </summary>
    public static bool TryPickBaseline(bool isTracked, float trackedBaseline, float written, float current, out float baseline)
    {
        if (isTracked && (MathF.Abs(current - written) < 0.0001f || !float.IsFinite(current)))
        {
            baseline = trackedBaseline;
            return true;
        }

        baseline = current;
        return float.IsFinite(current);
    }
}
