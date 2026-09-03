// kanban/in-progress/teams-mvp.md WI-8 — step definitions for
// Features/ScoringTeams.feature: the one Given/When/Then scenario that walks a
// team-classified competition end to end — teams and memberships (one
// non-contributing member, the defending-champion shape), protection, draw
// acceptance, scores captured out of order across rounds, and finalisation —
// asserting at every step that the team standings stay readable and correct
// from whatever scores are present (NFR-4: results derive from what is
// present, nothing gates on anything else).
//
// Every step drives real HTTP against the real Soarscore.Api
// (AcceptanceFixture.Client), the discipline CapturingAScoreSteps.cs
// established; a self-contained [Binding] class with its own step phrasings,
// because Reqnroll binds regexes assembly-wide and a shared regex across two
// classes is an ambiguous match.
//
// F5J (30-f5j) throughout, for the same reasons the sibling steps classes
// choose it: literal MinPerGroup 6, so the 6-competitor field draws to exactly
// one group per round; Validity.MinRounds 4, so 4 flown rounds make the
// contest finalisable; the drop needs 5 completed rounds, so no scenario here
// trips one and the final aggregates are plain sums. Raw score == flightTime
// (every other captured metric contributes zero — the file-header convention
// of ScoringACompetitionSteps.cs), and competitor 1's 500 s wins every
// completed round, so a completed round normalises to exactly
// 2 x flightTime and the finished aggregate is exactly 8 x it — the literal
// table the finished-standings Then pins.
//
// Mid-contest, a partial group normalises against whoever is present, so
// individual scores move as later scores arrive. The standings verification
// deliberately does NOT re-derive that arithmetic: it reads the individual
// leaderboard (/competition-result — the same scoring path the standings
// handler runs, and the layer the classification is downstream of by design
// principle 2) and recomputes the classification from it, mirroring
// TeamClassificationEngine's rules. That keeps the oracle independent of
// normalisation arithmetic while still checking every field of the derived
// section: contributors (with scores and placings), total, placing,
// placing sum, best individual placing, and every member's contribution
// state — after every single capture, not just at the checkpoints.

using AwesomeAssertions;
using Reqnroll;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;

namespace Soarscore.Acceptance.Tests.Steps;

[Binding]
public sealed class ScoringTeamsSteps
{
    private static HttpClient Client => AcceptanceFixture.Client;

    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    // The fixture's flight times by competitor ordinal — competitor 1 the
    // fastest (500, the group winner-anchor every completed round), each
    // later competitor 50 s slower. Raw == flightTime (this file's header), so
    // a completed round normalises to exactly 2 x flightTime and the finished
    // aggregate to exactly 8 x it — the literal table the finished-standings
    // Then pins. Mid-contest partials are whatever the leaderboard says; the
    // verification reads it rather than recomputing normalisation.
    private static readonly decimal[] FlightTimes = [500m, 450m, 400m, 350m, 300m, 250m];

    // The trickle order, scripted once and shared by all three When steps —
    // NFR-4's chaotic field with nothing imposed on it: round 2's scores
    // arrive before any of round 1's, all four rounds are interleaved from
    // the first capture, and the rounds COMPLETE in a scrambled order too
    // (2, then 1, then 3, then 4). The batch boundaries make the two
    // mid-contest checkpoints meaningful partial states: after the first six,
    // Harrier competitor 2 and Falcon competitor 5 hold no score at all and
    // every Falcon contributor flies; after fifteen, round 2 is complete
    // while round 1 has flown five of six — its winner (competitor 1's 500)
    // arriving last and re-normalising the five scores already on the board.
    private static readonly (int Round, int Competitor)[] FirstSix =
        [(2, 1), (2, 4), (4, 3), (2, 6), (1, 4), (2, 3)];

    private static readonly (int Round, int Competitor)[] NextNine =
        [(1, 2), (3, 5), (2, 2), (4, 1), (1, 6), (3, 2), (2, 5), (1, 3), (1, 1)];

    private static readonly (int Round, int Competitor)[] LastNine =
        [(4, 6), (3, 4), (1, 5), (4, 2), (3, 1), (3, 3), (4, 5), (3, 6), (4, 4)];

    private CompetitionId _competitionId = default;
    private int _rounds;

    private readonly List<CompetitorId> _competitors = [];
    private readonly Dictionary<CompetitorId, int> _ordinals = [];

    // The classification input the verification oracle re-derives from: teams
    // in definition order, each member with their contribution eligibility —
    // exactly what the table Given posts and what the engine reads.
    private readonly List<(string Name, List<(CompetitorId Ref, bool Contributes)> Members)> _teams = [];
    private readonly Dictionary<string, ScoringTeamId> _teamRefs = [];

    private (CompetitorId A, CompetitorId B) _protectedPair;

    private readonly Dictionary<(int Round, int Group), GroupId> _groupIds = new();

    // One standings-plus-leaderboard read per capture, recorded by the When
    // steps and verified by the Then steps — "available and correct after each
    // capture" as a fact about every capture, not a sampling.
    private sealed record StandingsSnapshot(
        int CaptureIndex,
        int RoundOrdinal,
        int CompetitorOrdinal,
        CompetitionScoreView Leaderboard,
        TeamStandingsView Standings);

    private readonly List<StandingsSnapshot> _snapshots = [];

    // The derived section as it stood when the field completed — the frozen
    // half's mirror for the declared-vs-derived comparison after finalisation.
    private TeamClassificationResult? _finishedStandings;

    // ---------------------------------------------------------------- Given

    [Given(@"^an F5J competition with (\d+) registered competitors$")]
    public async Task GivenAnF5JCompetitionWithRegisteredCompetitors(int count)
    {
        var contentHash = await ApiClient.PostCommandAsync<string>(
            Client, "/publish-class-definition", new PublishClassDefinition(F5JDefinition));

        // Person.IsPlausibleEmail rejects whitespace (Person.cs) — the same
        // hyphen-free per-scenario slug the other Steps classes use to keep
        // scenarios sharing one database from colliding.
        var slug = Guid.NewGuid().ToString("N");
        _competitionId = await ApiClient.PostCommandAsync<CompetitionId>(
            Client,
            "/create-competition",
            new CreateCompetition($"Teams Acceptance {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        for (var i = 0; i < count; i++)
        {
            var email = $"pilot-teams-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await ApiClient.PostCommandAsync<PersonId>(
                Client, "/register-person", new RegisterPerson($"Pilot {i + 1}", new ContactDetails { Email = email }, null));
            var competitorId = await ApiClient.PostCommandAsync<CompetitorId>(
                Client, "/register-competitor", new RegisterCompetitor(_competitionId, personId));
            _competitors.Add(competitorId);
            _ordinals[competitorId] = i + 1;
        }
    }

    // One row per membership: the team is defined on first appearance
    // (DefineScoringTeam), then every row assigns one competitor to it with
    // their contribution eligibility. The defending-champion row — competitor
    // 1, contributes no — is the story's shape: they fly alongside the
    // Harriers, fastest of anyone, and never count toward the Harriers' total.
    [Given(@"^the scoring teams are defined with these memberships$")]
    public async Task GivenTheScoringTeamsAreDefinedWithTheseMemberships(Table table)
    {
        foreach (var row in table.Rows)
        {
            var name = row["team"];
            if (!_teamRefs.TryGetValue(name, out var teamRef))
            {
                teamRef = await ApiClient.PostCommandAsync<ScoringTeamId>(
                    Client, "/define-scoring-team", new DefineScoringTeam(_competitionId, name));
                _teamRefs[name] = teamRef;
                _teams.Add((name, []));
            }

            var competitorRef = Competitor(int.Parse(row["competitor"]));
            var contributes = row["contributes"] == "yes";

            await ApiClient.PostCommandAsync<CompetitionId>(
                Client, "/assign-scoring-team-membership",
                new AssignScoringTeamMembership(_competitionId, competitorRef, teamRef, contributes));

            _teams.Single(t => t.Name == name).Members.Add((competitorRef, contributes));
        }
    }

    [Given(@"^team classification is enabled with the bestThreeScoreSum method$")]
    public async Task GivenTeamClassificationIsEnabledWithTheBestThreeScoreSumMethod()
    {
        // The command carries enabled + by only: the MVP's closed method
        // vocabulary has exactly one member, which the decide emits literally
        // (Competition.ConfigureTeamClassification) — "with the
        // bestThreeScoreSum method" names the policy the configuration
        // declares, and the standings verification asserts it on every read.
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/configure-team-classification",
            new ConfigureTeamClassification(_competitionId, Enabled: true, By: "the contest director"));
    }

    // Membership edits are refused once a phase exists (the
    // addProtectionMember.drawExists gate), so protection is set up before the
    // draw — "protection setup as needed for a draw". The pair is drawn from
    // two different teams on purpose: protection and scoring teams are
    // unrelated vocabularies (a competitor holds one scoring-team membership
    // and any number of protection-group memberships).
    [Given(@"^a protection group pairs competitors (\d+) and (\d+)$")]
    public async Task GivenAProtectionGroupPairsCompetitors(int aOrdinal, int bOrdinal)
    {
        var groupRef = await ApiClient.PostCommandAsync<ProtectionGroupId>(
            Client, "/define-protection-group", new DefineProtectionGroup(_competitionId, "Helper pair"));

        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/add-protection-group-member",
            new AddProtectionGroupMember(_competitionId, Competitor(aOrdinal), groupRef));
        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/add-protection-group-member",
            new AddProtectionGroupMember(_competitionId, Competitor(bOrdinal), groupRef));

        _protectedPair = (Competitor(aOrdinal), Competitor(bOrdinal));
    }

    [Given(@"^the preliminary phase is drawn for (\d+) rounds and accepted$")]
    public async Task GivenThePreliminaryPhaseIsDrawnForRoundsAndAccepted(int rounds)
    {
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/draw-phase", new DrawPhase(_competitionId, rounds));
        // D4: flying starts at acceptance, not at the draw.
        await ApiClient.PostCommandAsync<CompetitionId>(Client, "/accept-draw", new AcceptDraw(_competitionId));
        _rounds = rounds;
    }

    // ----------------------------------------------------------------- When

    [When(@"^the first six scores trickle in, round 2's before round 1's$")]
    public async Task WhenTheFirstSixScoresTrickleIn() => await TrickleInAsync(FirstSix);

    [When(@"^nine more trickle in, still skipping across rounds$")]
    public async Task WhenNineMoreTrickleIn() => await TrickleInAsync(NextNine);

    [When(@"^the last nine trickle in, completing the field$")]
    public async Task WhenTheLastNineTrickleIn() => await TrickleInAsync(LastNine);

    [When(@"^the contest director closes the flown rounds and finalises the competition$")]
    public async Task WhenTheContestDirectorClosesTheFlownRoundsAndFinalisesTheCompetition()
    {
        // F5J's validity gate counts rounds "flown to a result" — a round is
        // one only when its task-round is complete, so the CD closes each
        // before finalising (the ClosingACompetitionSteps precedent).
        for (var roundOrdinal = 1; roundOrdinal <= _rounds; roundOrdinal++)
        {
            await ApiClient.PostCommandAsync<CompetitionId>(
                Client, "/complete-task-round", new CompleteTaskRound(_competitionId, 0, roundOrdinal, 1));
        }

        await ApiClient.PostCommandAsync<CompetitionId>(
            Client, "/finalise-competition", new FinaliseCompetition(_competitionId, "the contest director"));
    }

    // ----------------------------------------------------------------- Then

    // F5J's literal MinPerGroup 6 draws the whole 6-competitor field as ONE
    // group per round, so no partition can separate the pair: the least-bad
    // draw co-groups it in every round (owner decision 5), and the read side
    // names the violations instead of refusing the draw. The CD reads this
    // and accepts anyway — the diagnostics exist for exactly that decision.
    [Then(@"^the protection diagnostics name the paired competitors in all (\d+) rounds$")]
    public async Task ThenTheProtectionDiagnosticsNameThePairedCompetitorsInAllRounds(int rounds)
    {
        var view = await ApiClient.GetAsync<DrawProtectionDiagnosticsView>(
            Client, $"/draw-diagnostics?competitionRef={_competitionId.Value}");

        view.Violations.Should().HaveCount(rounds);
        view.Violations.Select(v => v.RoundOrdinal).Should().Equal(Enumerable.Range(1, rounds));

        foreach (var violation in view.Violations)
        {
            violation.PhaseOrdinal.Should().Be(0);
            violation.TaskRoundOrdinal.Should().Be(1);
            violation.GroupOrdinal.Should().Be(1);
            new HashSet<CompetitorId> { violation.CompetitorA, violation.CompetitorB }
                .Should().BeEquivalentTo(new[] { _protectedPair.A, _protectedPair.B });
        }
    }

    // The NFR-4 checkpoint: standings exist and are exactly the
    // classification of whatever the leaderboard holds right now — partial
    // rounds, partial teams, members still scoreless, nothing gated.
    [Then(@"^the team standings derive correctly from those partial scores$")]
    public async Task ThenTheTeamStandingsDeriveCorrectlyFromThosePartialScores()
    {
        var leaderboard = await LeaderboardAsync();
        var standings = await StandingsAsync();

        standings.Derived.Should().NotBeNull("partial scores must still yield readable team standings (NFR-4)");

        VerifyDerived(standings.Derived!, leaderboard, "the live partial standings");

        // The defending-champion shape, asserted mid-contest where it matters:
        // their score derives like anyone else's, but they never count.
        var champion = Competitor(1);
        var harriers = standings.Derived!.Standings.Single(s => s.Name == "Harriers");
        harriers.Members.Single(m => m.CompetitorRef == champion)
            .State.Should().Be(TeamContributionState.Ineligible);
        harriers.Contributors.Should().NotContain(c => c.CompetitorRef == champion);
    }

    [Then(@"^every standings read (?:so far|after every capture) has matched the scores present at its moment$")]
    public void ThenEveryStandingsReadHasMatchedTheScoresPresentAtItsMoment()
    {
        _snapshots.Should().NotBeEmpty();

        foreach (var snapshot in _snapshots)
        {
            snapshot.Standings.Derived.Should().NotBeNull(
                "capture {0} (round {1}, competitor {2}) must leave the standings readable (NFR-4)",
                snapshot.CaptureIndex, snapshot.RoundOrdinal, snapshot.CompetitorOrdinal);

            VerifyDerived(
                snapshot.Standings.Derived!,
                snapshot.Leaderboard,
                $"capture {snapshot.CaptureIndex} (round {snapshot.RoundOrdinal}, competitor {snapshot.CompetitorOrdinal})");
        }
    }

    // The finished contest, pinned literally: the individual ladder (each
    // completed round normalised to exactly 2 x flightTime against
    // competitor 1's 500 — this file's header) and the full classification
    // evidence on top of it. The independent re-derivation runs here too, so
    // the literal pins and the oracle hold each other honest.
    [Then(@"^the finished standings carry the full evidence, contributors and tie-breaks included$")]
    public async Task ThenTheFinishedStandingsCarryTheFullEvidence()
    {
        var leaderboard = await LeaderboardAsync();
        var standings = await StandingsAsync();
        standings.Derived.Should().NotBeNull();
        var derived = standings.Derived!;

        // The individual ladder the classification is downstream of: 4 rounds,
        // each 2 x flightTime (competitor 1's 500 wins every completed round),
        // no drop (F5J's drop needs 5 completed rounds) — aggregate 8 x time.
        leaderboard.Scores.Should().HaveCount(_competitors.Count);
        foreach (var score in leaderboard.Scores)
        {
            var ordinal = _ordinals[score.CompetitorRef];
            score.Disqualified.Should().BeFalse();
            score.Score.Should().Be(8m * FlightTimes[ordinal - 1]);
            score.Placing.Should().Be(ordinal);
        }

        derived.Method.Should().Be(TeamClassificationEngine.MethodBestThreeScoreSum);
        derived.SourceClassification.Should().Be(TeamClassificationEngine.SourceCompetitionFinalAggregate);

        derived.Standings.Should().HaveCount(2);

        var falcons = derived.Standings[0];
        var harriers = derived.Standings[1];

        // Order and totals: the Falcons' three contributors (competitors
        // 3, 4, 5) outrank the Harriers' two counting members — the champion's
        // 4000 is the field's best individual score and is in NO total.
        falcons.Name.Should().Be("Falcons");
        falcons.Placing.Should().Be(1);
        falcons.Total.Should().Be(8400m);
        falcons.PlacingSum.Should().Be(12);
        falcons.BestIndividualPlacing.Should().Be(3);
        falcons.Contributors.Should().BeEquivalentTo(new[]
        {
            new { CompetitorRef = Competitor(3), Score = 3200m, Placing = 3 },
            new { CompetitorRef = Competitor(4), Score = 2800m, Placing = 4 },
            new { CompetitorRef = Competitor(5), Score = 2400m, Placing = 5 },
        }, o => o.WithStrictOrdering());

        harriers.Name.Should().Be("Harriers");
        harriers.Placing.Should().Be(2);
        harriers.Total.Should().Be(5600m);
        harriers.PlacingSum.Should().Be(8);
        harriers.BestIndividualPlacing.Should().Be(2);
        harriers.Contributors.Should().BeEquivalentTo(new[]
        {
            new { CompetitorRef = Competitor(2), Score = 3600m, Placing = 2 },
            new { CompetitorRef = Competitor(6), Score = 2000m, Placing = 6 },
        }, o => o.WithStrictOrdering());
        harriers.Members.Should().BeEquivalentTo(new[]
        {
            new { CompetitorRef = Competitor(1), State = TeamContributionState.Ineligible },
            new { CompetitorRef = Competitor(2), State = TeamContributionState.Contributor },
            new { CompetitorRef = Competitor(6), State = TeamContributionState.Contributor },
        });

        _finishedStandings = derived;

        // And the independent re-derivation still agrees with all of it.
        VerifyDerived(derived, leaderboard, "the finished standings");
    }

    // The frozen half, read from the ONLY surface declared team results have
    // (the standings query's declared section — no general finalisation read
    // surface exists): field-for-field equal to the derived standings at that
    // moment, and the derivation itself undisturbed by the declaration.
    [Then(@"^the declared team results equal the derived standings at that moment$")]
    public async Task ThenTheDeclaredTeamResultsEqualTheDerivedStandingsAtThatMoment()
    {
        var standings = await StandingsAsync();

        standings.Declared.Should().NotBeNull("finalisation is the declared section's only read surface");
        standings.Derived.Should().NotBeNull();

        var derived = standings.Derived!.Standings;
        var declared = standings.Declared!.Value;

        declared.Should().HaveCount(derived.Length);

        for (var i = 0; i < declared.Length; i++)
        {
            var d = declared[i];
            var s = derived[i];

            d.TeamRef.Should().Be(s.TeamRef);
            d.Name.Should().Be(s.Name);
            d.Total.Should().Be(s.Total);
            d.Placing.Should().Be(s.Placing);
            d.PlacingSum.Should().Be(s.PlacingSum);
            d.BestIndividualPlacing.Should().Be(s.BestIndividualPlacing);

            d.Contributors.Should().HaveCount(s.Contributors.Length);
            for (var j = 0; j < d.Contributors.Length; j++)
            {
                d.Contributors[j].CompetitorRef.Should().Be(s.Contributors[j].CompetitorRef);
                d.Contributors[j].Score.Should().Be(s.Contributors[j].Score);
                d.Contributors[j].Placing.Should().Be(s.Contributors[j].Placing);
            }
        }

        // Invariant B for teams: the declaration froze what the standings said
        // and the re-derivation still lands on it — the declared-vs-derived
        // read shows a divergence only when a correction lands after the fact,
        // and none has here.
        standings.Derived.Should().BeEquivalentTo(_finishedStandings, o => o.WithStrictOrdering());
    }

    // --------------------------------------------------------------- helpers

    private async Task TrickleInAsync((int Round, int Competitor)[] batch)
    {
        foreach (var (roundOrdinal, competitorOrdinal) in batch)
        {
            var groupId = await ResolveGroupIdAsync(roundOrdinal);
            var entryId = await ApiClient.PostCommandAsync<EntryId>(
                Client, "/open-entry",
                new OpenEntry(_competitionId, 0, roundOrdinal, 1, groupId, Competitor(competitorOrdinal)));

            await ApiClient.PostCommandAsync<EntryId>(Client, "/open-flight", new OpenFlight(entryId));

            // Every metric but flightTime is fixed to a value contributing
            // zero to the raw score (this file's header) — raw == flightTime.
            await CaptureAsync(entryId, "flightTime", MeasuredValue.Of(FlightTimes[competitorOrdinal - 1]));
            await CaptureAsync(entryId, "startHeight", MeasuredValue.Of(0m));
            await CaptureAsync(entryId, "startHeightRecorded", MeasuredValue.Of(true));
            await CaptureAsync(entryId, "overflySeconds", MeasuredValue.Of(0m));
            await CaptureAsync(entryId, "touchedByCompetitor", MeasuredValue.Of(false));
            await CaptureAsync(entryId, "landingDistance", MeasuredValue.Of(100m)); // beyond the last row -> Rest(0)

            // The NFR-4 read, right after the capture: whatever the standings
            // say now must be the classification of whatever the leaderboard
            // says now. Both are recorded; the Then steps verify every one.
            _snapshots.Add(new StandingsSnapshot(
                _snapshots.Count + 1,
                roundOrdinal,
                competitorOrdinal,
                await LeaderboardAsync(),
                await StandingsAsync()));
        }
    }

    private CompetitorId Competitor(int ordinal) => _competitors[ordinal - 1];

    private async Task<CompetitionScoreView> LeaderboardAsync() =>
        await ApiClient.GetAsync<CompetitionScoreView>(Client, $"/competition-result?competitionRef={_competitionId.Value}");

    private async Task<TeamStandingsView> StandingsAsync() =>
        await ApiClient.GetAsync<TeamStandingsView>(Client, $"/competition-team-result?competitionRef={_competitionId.Value}");

    private async Task<GroupId> ResolveGroupIdAsync(int roundOrdinal)
    {
        var key = (roundOrdinal, 1);
        if (_groupIds.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var view = await ApiClient.GetAsync<CompetitionView>(Client, $"/competition?id={_competitionId.Value}");
        var phase = view.Competition.Phases.Single();
        var round = phase.Rounds.Single(r => r.Ordinal == roundOrdinal);
        var group = round.TaskRounds.Single().Groups.Single();

        // F5J's literal MinPerGroup 6 draws the 6-competitor field as one
        // group per round — asserted rather than assumed, because every
        // expected value (normalisation per group, the inseparable protected
        // pair, the literal final table) relies on this shape.
        group.CompetitorRefs.Should().HaveCount(_competitors.Count);

        _groupIds[key] = group.Id;
        return group.Id;
    }

    /// <summary>
    /// The oracle: the team classification the given leaderboard implies,
    /// computed here from the classification rules the story settled — never
    /// from the engine's own code. The leaderboard is the individual
    /// classification the standings derive from (design principle 2), so
    /// "the standings are correct" means exactly: equal to this, field for
    /// field, at every moment, from whatever has arrived (NFR-4).
    /// </summary>
    private void VerifyDerived(TeamClassificationResult derived, CompetitionScoreView leaderboard, string because)
    {
        derived.Method.Should().Be(TeamClassificationEngine.MethodBestThreeScoreSum, because);
        derived.SourceClassification.Should().Be(TeamClassificationEngine.SourceCompetitionFinalAggregate, because);

        var expected = ExpectedStandings(leaderboard);
        derived.Standings.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering(), because);
    }

    private List<TeamStanding> ExpectedStandings(CompetitionScoreView leaderboard)
    {
        var rows = leaderboard.Scores.ToDictionary(s => s.CompetitorRef);

        var standings = new List<TeamStanding>();

        foreach (var (name, members) in _teams)
        {
            // A member counts only when the leaderboard carries their score
            // (an aggregate — absent until their first captured coordinate),
            // they are unflagged and hold a placing (on this view, exactly
            // "not disqualified" — a null Placing has no other source), and
            // their eligibility says they contribute. The defending champion
            // filters out here, whatever they scored.
            var candidates = new List<(CompetitorId Ref, decimal Score, int Placing)>();
            foreach (var (competitorRef, contributes) in members)
            {
                if (!rows.TryGetValue(competitorRef, out var row))
                {
                    continue;
                }

                if (row.Disqualified || row.Placing is not { } placing)
                {
                    continue;
                }

                if (!contributes)
                {
                    continue;
                }

                candidates.Add((competitorRef, row.Score, placing));
            }

            // The three highest scores, equal scores refined by the better
            // placing then the competitor id — the engine's deterministic
            // ladder, so no input order can decide.
            candidates.Sort((a, b) =>
            {
                var c = b.Score.CompareTo(a.Score);
                if (c != 0)
                {
                    return c;
                }

                c = a.Placing.CompareTo(b.Placing);
                if (c != 0)
                {
                    return c;
                }

                return a.Ref.Value.CompareTo(b.Ref.Value);
            });

            var chosen = candidates.Take(3).ToList();
            var chosenRefs = chosen.Select(c => c.Ref).ToHashSet();

            standings.Add(new TeamStanding
            {
                TeamRef = _teamRefs[name],
                Name = name,
                Total = chosen.Sum(c => c.Score),
                Placing = 0, // assigned below, shared-place convention
                PlacingSum = chosen.Sum(c => c.Placing),
                BestIndividualPlacing = chosen.Count == 0 ? null : chosen.Min(c => c.Placing),
                Contributors = [.. chosen.Select(c => new TeamContributor
                {
                    CompetitorRef = c.Ref,
                    Score = c.Score,
                    Placing = c.Placing,
                })],
                Members = [.. members
                    .OrderBy(m => m.Ref.Value)
                    .Select(m => new TeamMemberContribution
                    {
                        CompetitorRef = m.Ref,
                        State = StateOf(rows, m, chosenRefs),
                    })],
            });
        }

        // Team order: Total DESC -> PlacingSum ASC -> BestIndividualPlacing
        // ASC (nulls last) -> name, then the team id as the total-order
        // formality — the engine's own ladder, mirrored.
        standings.Sort((a, b) =>
        {
            var c = b.Total.CompareTo(a.Total);
            if (c != 0)
            {
                return c;
            }

            c = a.PlacingSum.CompareTo(b.PlacingSum);
            if (c != 0)
            {
                return c;
            }

            c = (a.BestIndividualPlacing, b.BestIndividualPlacing) switch
            {
                (null, null) => 0,
                (null, _) => 1,
                (_, null) => -1,
                (var x, var y) => x.Value.CompareTo(y.Value),
            };
            if (c != 0)
            {
                return c;
            }

            c = string.CompareOrdinal(a.Name, b.Name);
            if (c != 0)
            {
                return c;
            }

            return a.TeamRef.Value.CompareTo(b.TeamRef.Value);
        });

        // Shared places: teams equal on every rung share the place, and the
        // next place skips the group size — RankingEngine's own convention.
        var placedStandings = new List<TeamStanding>(standings.Count);
        int place = 1;
        int i = 0;
        while (i < standings.Count)
        {
            int j = i + 1;
            while (j < standings.Count && SameRungs(standings[i], standings[j]))
            {
                j++;
            }

            for (var k = i; k < j; k++)
            {
                placedStandings.Add(standings[k] with { Placing = place });
            }

            place += j - i;
            i = j;
        }

        return placedStandings;
    }

    private static TeamContributionState StateOf(
        IReadOnlyDictionary<CompetitorId, CompetitorFinalScoreView> rows,
        (CompetitorId Ref, bool Contributes) member,
        HashSet<CompetitorId> chosen)
    {
        if (!rows.TryGetValue(member.Ref, out var row))
        {
            return TeamContributionState.NoScoreYet;
        }

        if (row.Disqualified)
        {
            return TeamContributionState.Disqualified;
        }

        if (!member.Contributes)
        {
            return TeamContributionState.Ineligible;
        }

        return chosen.Contains(member.Ref)
            ? TeamContributionState.Contributor
            : TeamContributionState.EligibleNotCounting;
    }

    private static bool SameRungs(TeamStanding a, TeamStanding b) =>
        a.Total == b.Total
        && a.PlacingSum == b.PlacingSum
        && a.BestIndividualPlacing.Equals(b.BestIndividualPlacing);

    private static async Task CaptureAsync(EntryId entryId, string metric, MeasuredValue value) =>
        await ApiClient.PostCommandAsync<EntryId>(
            Client, "/capture-measurement", new CaptureMeasurement(entryId, 1, metric, value));
}
