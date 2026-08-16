// kanban/in-progress/catalogue-choice-draws-plan.md WI-7 part 2 — step
// definitions for Features/DrawingACatalogueChoicePhase.feature. Same real-HTTP
// discipline as CapturingAScoreSteps.cs/ScoringACompetitionSteps.cs: a
// self-contained [Binding] class with its own Given/When/Then phrasing (a
// step regex shared verbatim across two Binding classes is ambiguous to
// Reqnroll, per the existing precedent's own note), so this class does not
// attempt to reuse CapturingAScoreSteps' "a published ... class definition"
// step text even though the two do the same thing underneath.

using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class DrawingACatalogueChoicePhaseSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private CompetitionId _competitionId;
    private List<string> _namedTaskRefs = [];
    private HttpResponseMessage? _drawResponse;

    [Given(@"^the F3K class is published and adopted by a competition with (\d+) registered competitors$")]
    public async Task GivenTheF3KClassIsPublishedAndAdoptedByACompetitionWithRegisteredCompetitors(int count)
    {
        var definition = Corpus.All.Single(c => c.FileName == "10-f3k").Definition;
        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(definition));

        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Catalogue Draw {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-catalogue-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
        }
    }

    [When(@"^the CD draws the preliminary phase naming these tasks$")]
    public async Task WhenTheCDDrawsThePreliminaryPhaseNamingTheseTasks(Table table)
    {
        _namedTaskRefs = table.Rows.Select(row => row["task"]).ToList();
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/draw-phase", new DrawPhase(_competitionId, _namedTaskRefs.Count, _namedTaskRefs));
    }

    [When(@"^the CD attempts to draw the preliminary phase naming these tasks$")]
    public async Task WhenTheCDAttemptsToDrawThePreliminaryPhaseNamingTheseTasks(Table table)
    {
        _namedTaskRefs = table.Rows.Select(row => row["task"]).ToList();
        _drawResponse = await ApiClient.PostCommandRawAsync(
            Client, "/draw-phase", new DrawPhase(_competitionId, _namedTaskRefs.Count, _namedTaskRefs));
    }

    [Then(@"^each round is scheduled with its named task$")]
    public async Task ThenEachRoundIsScheduledWithItsNamedTask()
    {
        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var rounds = view.Competition.Phases.Single().Rounds.OrderBy(r => r.Ordinal).ToList();

        rounds.Should().HaveCount(_namedTaskRefs.Count);
        rounds.Select(r => r.TaskRounds.Single().TaskRef).Should().BeEquivalentTo(_namedTaskRefs, o => o.WithStrictOrdering());
    }

    // The domain's taskSelectionNotDistinct message states the catalogue
    // size, not the specific repeated code (Competition.cs's DrawPhase —
    // "one code, and a message that states the catalogue size, is enough" per
    // the plan's own WI-2 note) — so this asserts the stable failure code and
    // that the message names the actual rule broken, not a literal echo of
    // which task repeated.
    [Then(@"^the draw is refused because the tasks are not distinct$")]
    public async Task ThenTheDrawIsRefusedBecauseTheTasksAreNotDistinct()
    {
        _drawResponse.Should().NotBeNull();
        _drawResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await _drawResponse.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Options);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("drawPhase.taskSelectionNotDistinct");
        problem.Detail.Should().Contain("different task every round");
    }
}
