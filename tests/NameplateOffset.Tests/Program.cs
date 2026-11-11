// Run: dotnet run --project tests/NameplateOffset.Tests  (exit code = number of failures)
using System;
using NameplateOffset;

var fail = 0;
void Check(bool ok, string name)
{
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
    fail += ok ? 0 : 1;
}

foreach (var s in new[] { "NaN", "Infinity", "-Infinity", "1e40", "abc", "" })
    Check(!OffsetRules.TryParse(s, out _), $"TryParse rejects '{s}'");
Check(OffsetRules.TryParse("0.35", out var a) && a == 0.35f, "TryParse accepts 0.35");
Check(OffsetRules.TryParse("9", out var b) && b == OffsetRules.Max, "TryParse clamps 9 to Max");

Check(OffsetRules.Sanitize(float.NaN) == 0f && OffsetRules.Sanitize(float.NegativeInfinity) == 0f, "Sanitize NaN/Inf -> 0");
Check(OffsetRules.Sanitize(10f) == OffsetRules.Max && OffsetRules.Sanitize(-9f) == OffsetRules.Min, "Sanitize clamps");
Check(OffsetRules.Sanitize(0.5f) == 0.5f, "Sanitize keeps in-range value");

// A value seen for the first time is adopted at once; NaN before adoption means "leave alone".
var fresh = new OffsetRules.PlateState();
Check(fresh.TryAdopt(0.5f) && fresh.BaselineY == 0.5f, "fresh state adopts first finite value");
Check(!new OffsetRules.PlateState().TryAdopt(float.NaN), "fresh state skips NaN");
Check(new OffsetRules.PlateState().TryAdopt(float.PositiveInfinity) == false, "fresh state skips Infinity");

// A plate we already own keeps its baseline while the client's value wobbles (the reported jitter).
var noise = new OffsetRules.PlateState { BaselineY = 1f, WrittenY = 1.35f, Adopted = true };
var moved = false;
for (var frame = 0; frame < 60; frame++)
{
    var live = frame % 2 == 0 ? 1.35f + 0.02f : 1.35f - 0.02f;
    noise.TryAdopt(live);
    moved |= noise.BaselineY != 1f;
}
Check(!moved, "alternating per-frame noise never moves the baseline");

// A real change (mount, transform) is adopted once it has held for StableFrames frames, not before.
var mount = new OffsetRules.PlateState { BaselineY = 1f, WrittenY = 1.35f, Adopted = true };
for (var frame = 1; frame < OffsetRules.StableFrames; frame++)
{
    mount.TryAdopt(2.5f);
    Check(mount.BaselineY == 1f, $"held back on frame {frame} of the change");
}

mount.TryAdopt(2.5f);
Check(mount.BaselineY == 2.5f, "adopted after the change held for StableFrames frames");

// Our own written value (float rounding aside) is never mistaken for a game change.
var own = new OffsetRules.PlateState { BaselineY = 1f, WrittenY = 1.35f, Adopted = true };
own.TryAdopt(1.35f);
Check(own.BaselineY == 1f, "reading back our own value keeps the baseline");

// Garbage after adoption keeps the baseline instead of poisoning the plate.
var garbage = new OffsetRules.PlateState { BaselineY = 1f, WrittenY = 1.35f, Adopted = true };
Check(garbage.TryAdopt(float.NaN) && garbage.BaselineY == 1f, "tracked state keeps baseline on NaN");

// A change interrupted by a different value restarts the latch: StableFrames-1 of A, then B, then A again.
var interrupted = new OffsetRules.PlateState { BaselineY = 1f, WrittenY = 1.35f, Adopted = true };
for (var frame = 1; frame < OffsetRules.StableFrames; frame++)
    interrupted.TryAdopt(2.5f);
interrupted.TryAdopt(3f);
interrupted.TryAdopt(2.5f);
Check(interrupted.BaselineY == 1f, "an interrupted change does not carry its old frame count");
for (var frame = 1; frame < OffsetRules.StableFrames; frame++)
    interrupted.TryAdopt(2.5f);
Check(interrupted.BaselineY == 2.5f, "the restarted change is adopted after StableFrames uninterrupted frames");

// Reading back our own value mid-latch cancels the pending candidate.
var cancel = new OffsetRules.PlateState { BaselineY = 1f, WrittenY = 1.35f, Adopted = true };
for (var frame = 1; frame < OffsetRules.StableFrames; frame++)
    cancel.TryAdopt(2.5f);
cancel.TryAdopt(1.35f);
cancel.TryAdopt(2.5f);
Check(cancel.BaselineY == 1f, "our own value read mid-latch resets the pending change");

// An object first seen with NaN is adopted as soon as a finite value shows up (no latch before adoption).
var late = new OffsetRules.PlateState();
late.TryAdopt(float.NaN);
Check(late.TryAdopt(0.8f) && late.BaselineY == 0.8f, "first finite value after NaN is adopted at once");

Console.WriteLine(fail == 0 ? "ALL PASS" : $"{fail} FAILED");
return fail;
