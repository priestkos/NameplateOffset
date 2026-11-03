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

Check(OffsetRules.TryPickBaseline(true, 1f, 1.35f, 1.35f, out var k) && k == 1f, "keeps baseline when game value is ours");
Check(OffsetRules.TryPickBaseline(true, 1f, 1.35f, 2f, out var g) && g == 2f, "adopts game's new finite value");
Check(OffsetRules.TryPickBaseline(true, 1f, 1.35f, float.NaN, out var n) && n == 1f, "tracked + NaN keeps old baseline");
Check(!OffsetRules.TryPickBaseline(false, 0f, 0f, float.NaN, out _), "untracked + NaN is skipped");
Check(!OffsetRules.TryPickBaseline(false, 0f, 0f, float.PositiveInfinity, out _), "untracked + Inf is skipped");
Check(OffsetRules.TryPickBaseline(false, 0f, 0f, 0.5f, out var u) && u == 0.5f, "untracked finite adopted");

Console.WriteLine(fail == 0 ? "ALL PASS" : $"{fail} FAILED");
return fail;
