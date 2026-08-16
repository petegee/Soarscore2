// kanban/completed/multi-backend-deployment.md WI-5 — the one query whose
// semantics genuinely differ across backends, pinned.
//
// `p.Name.Contains(name)` compiles on every Critter Stack store and means
// something slightly different on each: Marten emits a Postgres `LIKE`, Fisher a
// deliberately ordinal, case-sensitive `instr`. Both happen to be
// case-sensitive, so nothing was visibly broken — but nothing was decided
// either, and "the two stores agree today" is not a property you can rely on
// when neither promises it. A pilot-name search is exactly where a user expects
// case-insensitivity, and the Application layer's own test doubles
// (tests/Soarscore.Application.Tests/Shared/People/TestDoubles.cs) had been
// written OrdinalIgnoreCase all along — so the fakes and the real adapters
// disagreed, and every handler test was passing against the wrong one.
//
// These tests are the decision. They run against every supported backend for
// the same reason the rest of this project's suites do: a claim about behaviour
// that is only checked on one store is a claim about that store.
//
// Accent- and culture-sensitivity are deliberately NOT asserted. OrdinalIgnoreCase
// is an ASCII-range case fold on both stores and neither promises more; asserting
// that "ä" matches "Ä" would be asserting something we have not decided and the
// stores have not agreed. If a pilot with a diacritic in their name ever makes
// that matter, it is a new decision, not a bug in this one.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class NameSearchTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly DateTimeOffset At = new(2026, 8, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Person_name_search_is_case_insensitive_and_matches_a_substring()
    {
        // A name whose casing is mixed in a way no query below reproduces, so a
        // match can only come from folding case rather than from luck.
        var id = PersonId.New();
        await fixture.EventStore.AppendAsync(
            id.Value,
            ExpectedVersion.NoStream,
            [new PersonRegistered(id, "Amelia EARhart", new ContactDetails { Email = "amelia@name-search.test" }, null, At)],
            TestContext.Current.CancellationToken);

        foreach (var term in new[] { "EARhart", "earhart", "EARHART", "Amelia", "amelia ear" })
        {
            var found = await fixture.PeopleQuery.SearchByNameAsync(term, TestContext.Current.CancellationToken);
            found.Select(p => p.Id).Should().Contain(id, $"searching for '{term}' should find 'Amelia EARhart'");
        }
    }

    [Fact]
    public async Task Person_name_search_still_excludes_a_name_that_does_not_contain_the_term()
    {
        var id = PersonId.New();
        await fixture.EventStore.AppendAsync(
            id.Value,
            ExpectedVersion.NoStream,
            [new PersonRegistered(id, "Jean Batten", new ContactDetails { Email = "jean@name-search.test" }, null, At)],
            TestContext.Current.CancellationToken);

        // Case-insensitivity must not have widened into matching anything: these
        // share letters with the name but are not substrings of it.
        foreach (var term in new[] { "battan", "jeanbatten", "zzz" })
        {
            var found = await fixture.PeopleQuery.SearchByNameAsync(term, TestContext.Current.CancellationToken);
            found.Select(p => p.Id).Should().NotContain(id, $"searching for '{term}' should not find 'Jean Batten'");
        }
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresNameSearchTests(PostgresFixture fixture) : NameSearchTests<PostgresFixture>(fixture);

public sealed class SqliteNameSearchTests(SqliteFixture fixture) : NameSearchTests<SqliteFixture>(fixture);
