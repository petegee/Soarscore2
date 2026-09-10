// kanban/in-progress/gs-ledger-modes.md WI-1 — the ledger-mode knob both GS
// harnesses read, after AcceptanceFixture's SOARSCORE_TEST_STORE pattern.
//
//   strict (default) — a fixture/pair carrying any PENDING ledger entry fails
//     after the full comparison, with a rendered explanation naming every
//     divergence (where and how SoarScore splits from GS). Permanent entries
//     are reported, never failed: they are decided laws
//     (kanban/deferred-decisions.md R1/T1) or structural facts, not debt.
//   ledgered — the historical behaviour: parity subtracts the ledger and
//     asserts the remainder; the parallel-run verdict judges set-equality.
//     For local exploration; CI runs strict so a forgotten setting fails
//     loudly rather than silently tolerating divergence.

namespace Soarscore.Acceptance.Tests.Support.Gliderscore;

public enum GsLedgerMode
{
    Strict,
    Ledgered,
}

public static class GsLedgerModeReader
{
    public const string EnvironmentVariableName = "SOARSCORE_GS_LEDGER_MODE";

    /// <summary>The mode the run asked for — strict unless overridden.</summary>
    public static GsLedgerMode FromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? "strict";

        return value.Trim().ToLowerInvariant() switch
        {
            "strict" => GsLedgerMode.Strict,
            "ledgered" => GsLedgerMode.Ledgered,
            _ => throw new InvalidOperationException(
                $"Unknown {EnvironmentVariableName} '{value}'. Valid values are 'strict' and 'ledgered'."),
        };
    }
}
