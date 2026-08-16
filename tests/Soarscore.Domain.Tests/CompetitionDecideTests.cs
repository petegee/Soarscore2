using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.Decide"/> (WI-2,
/// kanban/completed/create-competition-steel-thread-plan.md) — mirrors
/// PersonDecideTests's style. AdoptedRules validity is deliberately not
/// exercised here (the plan explicitly excludes it from this decide
/// function's scope); only that it passes through the resulting event
/// unchanged is asserted.
/// </summary>
public class CompetitionDecideTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void Decide_with_valid_input_succeeds_with_the_expected_event()
    {
        var id = CompetitionId.New();
        var adoptedRules = SampleAdoptedRules();
        var startDate = new DateOnly(2026, 3, 14);
        var endDate = new DateOnly(2026, 3, 15);
        var at = DateTimeOffset.UtcNow;

        var result = Competition.Decide(
            id, "Club Champs 2026", "Auckland", startDate, endDate, "1.0.0", adoptedRules, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.Name.Should().Be("Club Champs 2026");
        result.Value.Location.Should().Be("Auckland");
        result.Value.StartDate.Should().Be(startDate);
        result.Value.EndDate.Should().Be(endDate);
        result.Value.EvaluatorVersion.Should().Be("1.0.0");
        result.Value.AdoptedRules.Should().BeSameAs(adoptedRules);
        result.Value.At.Should().Be(at);
    }

    [Fact]
    public void Decide_with_equal_start_and_end_dates_succeeds()
    {
        var sameDay = new DateOnly(2026, 3, 14);

        var result = Competition.Decide(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            sameDay, sameDay, "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_with_a_blank_name_fails_with_a_stable_code(string blankName)
    {
        var result = Competition.Decide(
            CompetitionId.New(), blankName, "Auckland",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.name.blank");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_with_a_blank_location_fails_with_a_stable_code(string blankLocation)
    {
        var result = Competition.Decide(
            CompetitionId.New(), "Club Champs 2026", blankLocation,
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.location.blank");
    }

    [Fact]
    public void Decide_with_start_date_after_end_date_fails_with_a_stable_code()
    {
        var result = Competition.Decide(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 14),
            "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.dates.invalid");
    }

    private static Competition SampleCompetition() =>
        Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow));

    /// <summary>
    /// A minimal drawn phase — just enough for RegisterCompetitor's
    /// field-freeze check (!Phases.IsEmpty) to see a non-empty Phases array.
    /// Nothing reads inside Rounds/TaskRounds for that check, so both are left
    /// empty.
    /// </summary>
    private static Competition SampleCompetitionWithDrawnPhase()
    {
        var draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" };
        var phase = new Phase
        {
            Type = PhaseType.Preliminary,
            Ordinal = 1,
            Draw = draw,
            Rounds = ImmutableArray<Round>.Empty,
        };

        return SampleCompetition() with { Phases = [phase] };
    }

    [Fact]
    public void RegisterCompetitor_with_a_new_person_succeeds_and_allocates_number_one()
    {
        var competition = SampleCompetition();
        var id = CompetitorId.New();
        var personRef = PersonId.New();
        var at = DateTimeOffset.UtcNow;

        var result = competition.RegisterCompetitor(id, personRef, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Competitor.Id.Should().Be(id);
        result.Value.Competitor.PersonRef.Should().Be(personRef);
        result.Value.Competitor.CompetitorNumber.Should().Be(1);
        result.Value.Competitor.RegisteredAt.Should().Be(at);
        result.Value.Competitor.WithdrawnAt.Should().BeNull();
        result.Value.At.Should().Be(at);
    }

    [Fact]
    public void RegisterCompetitor_allocates_numbers_1_2_3_across_successive_registrations()
    {
        var competition = SampleCompetition();

        var first = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), DateTimeOffset.UtcNow);
        competition = competition.Apply(first.Value);

        var second = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), DateTimeOffset.UtcNow);
        competition = competition.Apply(second.Value);

        var third = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), DateTimeOffset.UtcNow);

        first.Value.Competitor.CompetitorNumber.Should().Be(1);
        second.Value.Competitor.CompetitorNumber.Should().Be(2);
        third.Value.Competitor.CompetitorNumber.Should().Be(3);
    }

    [Fact]
    public void RegisterCompetitor_with_an_already_registered_person_fails_with_a_stable_code()
    {
        var competition = SampleCompetition();
        var personRef = PersonId.New();
        var first = competition.RegisterCompetitor(CompetitorId.New(), personRef, DateTimeOffset.UtcNow);
        competition = competition.Apply(first.Value);

        var result = competition.RegisterCompetitor(CompetitorId.New(), personRef, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.competitor.alreadyRegistered");
    }

    [Fact]
    public void RegisterCompetitor_against_a_field_with_a_drawn_phase_fails_with_a_stable_code()
    {
        var competition = SampleCompetitionWithDrawnPhase();

        var result = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.field.frozen");
    }

    [Fact]
    public void WithdrawCompetitor_against_a_field_with_a_drawn_phase_still_succeeds()
    {
        var competition = SampleCompetition();
        var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), DateTimeOffset.UtcNow);
        competition = competition.Apply(registered.Value);
        competition = competition.Apply(new PhaseDrawn(
            1, PhaseType.Preliminary,
            new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" },
            [],
            DateTimeOffset.UtcNow));

        var result = competition.WithdrawCompetitor(registered.Value.Competitor.Id, DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void WithdrawCompetitor_with_a_valid_competitor_succeeds()
    {
        var competition = SampleCompetition();
        var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), DateTimeOffset.UtcNow);
        competition = competition.Apply(registered.Value);
        var at = DateTimeOffset.UtcNow.AddMinutes(5);

        var result = competition.WithdrawCompetitor(registered.Value.Competitor.Id, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompetitorRef.Should().Be(registered.Value.Competitor.Id);
        result.Value.At.Should().Be(at);
    }

    [Fact]
    public void WithdrawCompetitor_with_an_unknown_competitor_fails_with_a_stable_code()
    {
        var competition = SampleCompetition();

        var result = competition.WithdrawCompetitor(CompetitorId.New(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.competitor.notFound");
    }

    [Fact]
    public void WithdrawCompetitor_already_withdrawn_fails_with_a_stable_code()
    {
        var competition = SampleCompetition();
        var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), DateTimeOffset.UtcNow);
        competition = competition.Apply(registered.Value);
        var withdrawn = competition.WithdrawCompetitor(registered.Value.Competitor.Id, DateTimeOffset.UtcNow);
        competition = competition.Apply(withdrawn.Value);

        var result = competition.WithdrawCompetitor(registered.Value.Competitor.Id, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.competitor.alreadyWithdrawn");
    }

    [Fact]
    public void Withdrawing_then_re_registering_the_same_person_is_rejected()
    {
        var competition = SampleCompetition();
        var personRef = PersonId.New();
        var registered = competition.RegisterCompetitor(CompetitorId.New(), personRef, DateTimeOffset.UtcNow);
        competition = competition.Apply(registered.Value);
        var withdrawn = competition.WithdrawCompetitor(registered.Value.Competitor.Id, DateTimeOffset.UtcNow);
        competition = competition.Apply(withdrawn.Value);

        var result = competition.RegisterCompetitor(CompetitorId.New(), personRef, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.competitor.alreadyRegistered");
    }
}
