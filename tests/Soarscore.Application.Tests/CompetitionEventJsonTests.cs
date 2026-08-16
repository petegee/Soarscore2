using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests;

public class CompetitionEventJsonTests
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

    private static CompetitionCreated SampleCreatedEvent(DateTimeOffset? at = null) =>
        new(
            CompetitionId.New(),
            "Club Champs 2026",
            "Auckland",
            new DateOnly(2026, 3, 14),
            new DateOnly(2026, 3, 15),
            "1.0.0",
            SampleAdoptedRules(),
            at ?? DateTimeOffset.UtcNow);

    [Fact]
    public void Events_round_trip_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent created = SampleCreatedEvent();

        var json = JsonSerializer.Serialize(created, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<CompetitionCreated>();
    }

    [Fact]
    public void Created_event_serialises_with_the_kind_discriminator()
    {
        CompetitionEvent created = SampleCreatedEvent();

        var json = JsonSerializer.Serialize(created, SoarscoreEventJson.Options);

        json.Should().Contain("\"$kind\":\"competitionCreated\"");
    }

    private static Competitor SampleCompetitor() =>
        new()
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void CompetitorRegistered_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent registered = new CompetitorRegistered(SampleCompetitor(), DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(registered, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<CompetitorRegistered>();
    }

    [Fact]
    public void CompetitorRegistered_serialises_its_PersonId_as_a_nested_value_object_not_flattened()
    {
        var competitor = SampleCompetitor();
        CompetitionEvent registered = new CompetitorRegistered(competitor, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(registered, SoarscoreEventJson.Options);

        json.Should().Contain($"\"personRef\":{{\"value\":\"{competitor.PersonRef.Value}\"}}");
    }

    [Fact]
    public void CompetitorWithdrawn_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent withdrawn = new CompetitorWithdrawn(CompetitorId.New(), DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(withdrawn, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<CompetitorWithdrawn>();
    }

    // Two rounds, two distinct TaskRef codes — a catalogue-choice draw
    // shape (kanban/in-progress/catalogue-choice-draws-plan.md WI-5), so the
    // per-round TaskRef is actually covered by serialisation rather than
    // incidentally covered by a uniform one.
    private static PhaseDrawn SamplePhaseDrawnEvent(DateTimeOffset? at = null)
    {
        Round MakeRound(int ordinal, string taskRef)
        {
            var group1 = new Group
            {
                Id = GroupId.New(),
                Ordinal = 1,
                CompetitorRefs = [CompetitorId.New(), CompetitorId.New()],
            };
            var group2 = new Group
            {
                Id = GroupId.New(),
                Ordinal = 2,
                CompetitorRefs = [CompetitorId.New(), CompetitorId.New()],
            };
            var taskRound = new TaskRound
            {
                Ordinal = 1,
                State = TaskRoundState.Drawn,
                TaskRef = taskRef,
                Groups = [group1, group2],
            };

            return new Round { Ordinal = ordinal, TaskRounds = [taskRound] };
        }

        return new PhaseDrawn(
            PhaseOrdinal: 0,
            Type: PhaseType.Preliminary,
            Draw: new Draw { CreatedAt = at ?? DateTimeOffset.UtcNow, Status = "drawn" },
            Rounds: [MakeRound(1, "A"), MakeRound(2, "B")],
            At: at ?? DateTimeOffset.UtcNow);
    }

    [Fact]
    public void PhaseDrawn_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent drawn = SamplePhaseDrawnEvent();

        var json = JsonSerializer.Serialize(drawn, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<PhaseDrawn>();
    }

    [Fact]
    public void PhaseDrawn_round_trips_a_different_TaskRef_per_round()
    {
        CompetitionEvent drawn = SamplePhaseDrawnEvent();

        var json = JsonSerializer.Serialize(drawn, SoarscoreEventJson.Options);
        var reread = (PhaseDrawn)JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options)!;

        reread.Rounds.Select(r => r.TaskRounds[0].TaskRef).Should().Equal("A", "B");
    }

    private static ParameterBinding SampleParameterBinding(
        MeasuredValue value, DateTimeOffset? at = null, int? phaseOrdinal = null, int? roundOrdinal = null) =>
        new()
        {
            ParameterName = "minPerGroup",
            BoundValue = value,
            By = "CD",
            At = at ?? DateTimeOffset.UtcNow,
            PhaseOrdinal = phaseOrdinal,
            RoundOrdinal = roundOrdinal,
        };

    [Fact]
    public void ParameterBound_event_round_trips_through_SoarscoreEventJson_byte_for_byte_for_Number()
    {
        CompetitionEvent bound = new ParameterBound(SampleParameterBinding(MeasuredValue.Of(4m)));

        var json = JsonSerializer.Serialize(bound, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<ParameterBound>();
    }

    [Fact]
    public void ParameterBound_event_round_trips_through_SoarscoreEventJson_byte_for_byte_for_Flag()
    {
        CompetitionEvent bound = new ParameterBound(SampleParameterBinding(MeasuredValue.Of(true)));

        var json = JsonSerializer.Serialize(bound, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        reread.Should().BeOfType<ParameterBound>();
    }

    [Fact]
    public void ParameterBound_event_with_round_scope_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        // kanban/completed/per-round-parameter-bindings-plan.md's PhaseOrdinal/RoundOrdinal.
        CompetitionEvent bound = new ParameterBound(SampleParameterBinding(MeasuredValue.Of(420m), phaseOrdinal: 0, roundOrdinal: 3));

        var json = JsonSerializer.Serialize(bound, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        reemitted.Should().Be(json);
        var rereadBound = reread.Should().BeOfType<ParameterBound>().Subject;
        rereadBound.Binding.PhaseOrdinal.Should().Be(0);
        rereadBound.Binding.RoundOrdinal.Should().Be(3);
    }

    [Fact]
    public void Finalised_event_round_trips_with_decimal_aggregate_as_a_json_string()
    {
        var finalisation = new Finalisation
        {
            Scope = FinalisationScope.Competition,
            Revision = 1,
            By = "CD",
            At = DateTimeOffset.UtcNow,
            DeclaredResults =
            [
                new DeclaredResult
                {
                    CompetitorRef = CompetitorId.New(),
                    Aggregate = 599.9999999m,
                    Placing = 1,
                    Promoted = true,
                },
            ],
        };
        CompetitionEvent finalised = new Finalised(finalisation);

        var json = JsonSerializer.Serialize(finalised, SoarscoreEventJson.Options);

        json.Should().Contain("\"aggregate\":\"599.9999999\"");

        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);
        reemitted.Should().Be(json);
    }
}
