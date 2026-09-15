// authentication-and-authorisation.md WI-9, step 2 (D8): the Production
// guard, as failing tests. A release deployment that wanted to dodge auth —
// by leaving the mode unset, pinning "none", or pinning "mock" — gets a loud
// crash at boot (InvalidOperationException naming Soarscore:Auth:Mode), never
// a silently open or mock-keyed API. The misconfiguration cases (mock without
// its signing key, oidc without its domain/audience) throw in every
// environment, and "oidc" — and only "oidc" — is Production-legal.
//
// House style: direct Composition.Build calls, the RouteShapeTests /
// HandlerRegistrationTests pattern (fake, unreachable store connection —
// nothing here opens one; the guard throws before any store is built). The
// SigningKey override in the missing-key case is explicit so the test is
// deterministic whether or not the Api's appsettings.Development.json (which
// ships a development key) is present in the test's content root.

using AwesomeAssertions;
using Soarscore.Api;
using Xunit;

namespace Soarscore.ArchitectureTests;

public sealed class CompositionGuardTests
{
    // The same fake, unreachable store connection RouteShapeTests uses:
    // Marten needs it only to construct the DocumentStore (never opens a
    // connection), and Build-time must stay store-free for these tests —
    // the sqlite/Fisher store, by contrast, is constructed eagerly and would
    // turn the positive oidc case into a store test.
    private const string StoreArgs =
        "--Soarscore:Store=postgres --ConnectionStrings:Soarscore=Host=127.0.0.1;Port=1;Database=archtest;Username=archtest;Password=archtest";

    [Fact]
    public void Production_refuses_an_unset_auth_mode()
    {
        var boot = () => Composition.Build(
            ("--environment=Production " + StoreArgs).Split(' '));

        boot.Should().Throw<InvalidOperationException>()
            .WithMessage("*Soarscore:Auth:Mode*");
    }

    [Fact]
    public void Production_refuses_none_mode()
    {
        var boot = () => Composition.Build(
            ("--environment=Production --Soarscore:Auth:Mode=none " + StoreArgs).Split(' '));

        boot.Should().Throw<InvalidOperationException>()
            .WithMessage("*Soarscore:Auth:Mode*");
    }

    [Fact]
    public void Production_refuses_mock_mode()
    {
        var boot = () => Composition.Build(
            ("--environment=Production --Soarscore:Auth:Mode=mock "
             + "--Soarscore:Auth:Mock:SigningKey=Qu1Yb2FkcHdkcmFzc2lzdHM9Cg== " + StoreArgs).Split(' '));

        boot.Should().Throw<InvalidOperationException>()
            .WithMessage("*Soarscore:Auth:Mode*");
    }

    [Fact]
    public void Production_allows_oidc()
    {
        // The only Production-legal mode — and it boots without a live IdP:
        // JwtBearer's Authority discovery is per-request, never at Build.
        var boot = () => Composition.Build(
            ("--environment=Production --Soarscore:Auth:Mode=oidc "
             + "--Soarscore:Auth:Domain=example.eu.auth0.com --Soarscore:Auth:Audience=soarscore-api "
             + StoreArgs).Split(' '));

        boot.Should().NotThrow();
    }

    [Fact]
    public void Oidc_refuses_a_missing_domain_or_audience()
    {
        var missingBoth = () => Composition.Build(
            ("--environment=Development --Soarscore:Auth:Mode=oidc " + StoreArgs).Split(' '));

        missingBoth.Should().Throw<InvalidOperationException>()
            .WithMessage("*Soarscore:Auth:Domain*");

        var missingAudience = () => Composition.Build(
            ("--environment=Development --Soarscore:Auth:Mode=oidc --Soarscore:Auth:Domain=example.eu.auth0.com "
             + StoreArgs).Split(' '));

        missingAudience.Should().Throw<InvalidOperationException>()
            .WithMessage("*Soarscore:Auth:Audience*");
    }

    [Fact]
    public void Mock_refuses_a_missing_signing_key()
    {
        var boot = () => Composition.Build(
            ("--environment=Development --Soarscore:Auth:Mode=mock "
             + "--Soarscore:Auth:Mock:SigningKey= " + StoreArgs).Split(' '));

        boot.Should().Throw<InvalidOperationException>()
            .WithMessage("*Soarscore:Auth:Mock:SigningKey*");
    }

    [Fact]
    public void Mock_refuses_a_short_signing_key()
    {
        var boot = () => Composition.Build(
            ("--environment=Development --Soarscore:Auth:Mode=mock "
             + "--Soarscore:Auth:Mock:SigningKey=dG9vLXNob3J0Cg== " + StoreArgs).Split(' '));

        boot.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32*");
    }

    [Fact]
    public void An_unknown_mode_value_refuses_to_boot()
    {
        var boot = () => Composition.Build(
            ("--environment=Development --Soarscore:Auth:Mode=winging-it " + StoreArgs).Split(' '));

        boot.Should().Throw<InvalidOperationException>()
            .WithMessage("*Soarscore:Auth:Mode*");
    }
}
