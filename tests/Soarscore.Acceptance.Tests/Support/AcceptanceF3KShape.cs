// docs/plans/capture-a-score-steel-thread-plan.md WI-13, scenario 3's
// finding-3 regression at the acceptance level ("A launch before the working
// time is recorded, not refused").
//
// The scenario needs a published, DRAWABLE class whose Timing is Fixed, so
// an Entry can be opened with a real TimeWindow.Start and a flight can then
// be launched before it. The real corpus definition (Corpus.All, "10-f3k",
// SeedF3K.Definition) cannot be used for the "drawn preliminary phase" step:
// both of ITS phases use CompositionKind.ChooseFromCatalogue with more than
// one task (SeedF3K.cs's Preliminary/Flyoff phases), and
// Competition.DrawPhase explicitly rejects that shape
// ("drawPhase.unsupportedRoundComposition") — catalogue-choice rounds are
// this plan's own documented, out-of-scope gap ("Still gated, and not by
// this thread: Catalogue-choice rounds ... still the only thing between
// F3K/F5K and a draw").
//
// So this is a minimal, single-task class definition built from F3K's own
// real numbers — task D's shape (SeedF3K.cs: 10-minute fixed working time,
// 2 launches, flightTime truncated to 0.1 s per F3K.7, the same
// launchedInWorkingTime/landedWithinWindow void pair every F3K task
// inherits from task A) restructured as the sole task on a FixedSequence
// phase, which Competition.DrawPhase CAN schedule. It is not Corpus.All's
// SeedF3K and is not added there — this is test-local fixture data, the same
// category as OpenFlightDecideTests.cs's hand-built SampleWorkingTime/Entry,
// which proves the same finding-3 invariant at the domain level without
// touching the real corpus either.
//
// F3K.7 governs every Fixed-timing F3K task identically (a launch before the
// working time scores zero, it is never refused), and Entry.OpenFlight
// enforces this by NOT checking LaunchAt at all, for every class — the core
// architectural law forbids branching on which one. So this shape is a
// faithful acceptance-level exercise of that rule even though it is not the
// full 14-task published corpus definition.

using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Support;

public static class AcceptanceF3KShape
{
    private static TaskDefinition SingleTask => new()
    {
        Code = "D",
        Name = "Two flights",                                            // F3K.11.4 — same code/name as the real corpus's task D
        Metrics =
        [
            M.Number("flightTime", "s", RoundingMode.Truncate, 0.1m),    // F3K.7 — recorded to 0.1 s, truncated
            M.Flag("landedWithinWindow"),                                 // F3K.9.3
            M.Flag("launchedInWorkingTime"),                              // F3K.7
        ],
        Flights = new AllFlights(),
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,                                            // F3K.11.4 — 10-minute working time
            MaxLaunches = 2,
        },
        FlightValidWhen = P.All(
            P.Is("landedWithinWindow", true),
            P.Is("launchedInWorkingTime", true)),
        Score = [T.Rate("flightTime", 1, cap: 300)],                      // F3K.11.4 — 300 s cap, both flights summed
    };

    public static ClassDefinition Definition => new()
    {
        Name = "RC Hand-Launch Gliders (acceptance-test single-task shape)",
        FaiDesignation = "F3K",
        Version = "FAI F3 Soaring 2025 ed.2 (acceptance-test shape — see this file's header)",
        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,               // F3K.9.6
            OthersScore = ReflightSelection.BetterOf,                     // F3K.9.6
            MinNewGroupSize = 4,                                          // F3K.9.6
        },
        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                // Rounds left at its default (CompositionKind.FixedSequence,
                // TasksPerRound = 1) — exactly the shape Competition.DrawPhase
                // requires, and this phase's only task satisfies it.
                Validity = new() { MinRounds = 1 },
                Tasks = [SingleTask],
            },
        ],
    };
}
