using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Entry.RecordPenalty"/> —
/// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-3. One
/// example per defect code, the happy path asserting the payload round-trips
/// into the event, and a fold fact. Mirrors AmendMeasurementDecideTests.cs's
/// defect-per-fact shape; the entry is its own subject, so a competitor or
/// task-round coordinate is a defect rather than data.
///
/// Home to the penalty append-only invariant P3 (Entry half).
/// </summary>
public class RecordEntryPenaltyDecideTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly CompetitionId SampleCompetition = CompetitionId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static readonly ImmutableArray<PenaltyDefinition> SamplePenaltyDefinitions =
    [
        new() { InfractionType = "motorRestartInFlight", Effects = [new(PenaltyEffect.ZeroFlight)] },
        new() { InfractionType = "hitPersonOtherThanTimer", Effects = [new(PenaltyEffect.ZeroRound)] },
    ];

    private static Entry SampleEntry() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow));

    private static Penalty FlightPenalty() =>
        new() { InfractionType = "motorRestartInFlight", Scope = PenaltyScope.Flight };

    // ------------------------------------------------------------------- FAILURES

    [Fact]
    public void RecordPenalty_against_an_annulled_entry_fails_with_a_stable_code()
    {
        var entry = SampleEntry().Apply(SampleEntry().AnnulEntry("protest", "jury", DateTimeOffset.UtcNow).Value);

        var result = entry.RecordPenalty(FlightPenalty(), SamplePenaltyDefinitions);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.annulled");
    }

    [Fact]
    public void RecordPenalty_with_a_competition_scope_fails_with_a_stable_code()
    {
        var penalty = FlightPenalty() with { Scope = PenaltyScope.Competition };

        var result = SampleEntry().RecordPenalty(penalty, SamplePenaltyDefinitions);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.wrongScope");
    }

    [Fact]
    public void RecordPenalty_with_a_competitor_ref_fails_with_a_stable_code()
    {
        var penalty = FlightPenalty() with { CompetitorRef = CompetitorId.New() };

        var result = SampleEntry().RecordPenalty(penalty, SamplePenaltyDefinitions);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.subjectNotAllowed");
    }

    [Fact]
    public void RecordPenalty_with_a_task_round_coordinate_fails_with_a_stable_code()
    {
        var penalty = FlightPenalty() with { TaskRound = new TaskRoundCoordinate(0, 1, 1) };

        var result = SampleEntry().RecordPenalty(penalty, SamplePenaltyDefinitions);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.subjectNotAllowed");
    }

    [Fact]
    public void RecordPenalty_with_an_undeclared_infraction_type_fails_with_a_stable_code()
    {
        var penalty = FlightPenalty() with { InfractionType = "madeUp" };

        var result = SampleEntry().RecordPenalty(penalty, SamplePenaltyDefinitions);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.infractionTypeNotDeclared");
    }

    [Fact]
    public void RecordPenalty_with_a_blank_by_fails_with_a_stable_code()
    {
        var penalty = FlightPenalty() with { By = "   " };

        var result = SampleEntry().RecordPenalty(penalty, SamplePenaltyDefinitions);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.byBlank");
    }

    // -------------------------------------------------------------------- SUCCESS

    [Fact]
    public void RecordPenalty_succeeds_round_tripping_the_payload_into_the_event()
    {
        var by = "the scorer";
        var penalty = FlightPenalty() with { By = by };

        var result = SampleEntry().RecordPenalty(penalty, SamplePenaltyDefinitions);

        result.IsSuccess.Should().BeTrue();
        result.Value.Penalty.Should().Be(penalty);
        result.Value.Penalty.By.Should().Be(by);
    }

    [Fact]
    public void An_absent_by_is_accepted()
    {
        var result = SampleEntry().RecordPenalty(FlightPenalty(), SamplePenaltyDefinitions);

        result.IsSuccess.Should().BeTrue();
        result.Value.Penalty.By.Should().BeNull();
    }

    [Fact]
    public void Folding_a_recorded_penalty_grows_the_penalties_list()
    {
        var entry = SampleEntry();
        var decision = entry.RecordPenalty(FlightPenalty(), SamplePenaltyDefinitions);

        var folded = entry.Apply(decision.Value);

        folded.Penalties.Should().ContainSingle();
        folded.Penalties[0].Should().Be(FlightPenalty());
    }

    // ======================================================================= PROPERTY TESTS — P3

    // ------------------------------------------------------- P3
    // Penalties are append-only: for any sequence of n successful RecordPenalty
    // calls, the folded Penalties.Length == n and every payload is present in
    // order. Holds the fold and the decide function in agreement.

    private static readonly Gen<Penalty> EntryPenaltyGen =
        from byValue in Gen.OneOfConst<string?>(null, "the scorer", "the CD")
        select new Penalty { InfractionType = "motorRestartInFlight", Scope = PenaltyScope.Flight, By = byValue };

    [Fact]
    public void Penalties_are_append_only()
    {
        EntryPenaltyGen.Array[1, 5].Sample(penalties =>
        {
            var entry = SampleEntry();

            foreach (var penalty in penalties)
            {
                var decision = entry.RecordPenalty(penalty, SamplePenaltyDefinitions);
                decision.IsSuccess.Should().BeTrue();
                entry = entry.Apply(decision.Value);
            }

            entry.Penalties.Length.Should().Be(penalties.Length);
            entry.Penalties.Should().Equal(penalties);
        });
    }
}
