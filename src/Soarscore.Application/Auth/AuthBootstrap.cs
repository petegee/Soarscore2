// First-organiser bootstrap — authentication-and-authorisation.md WI-7 (D3):
// the config-listed email addresses whose holders become Organiser at
// sign-in/link time (LinkSignIn's bootstrap step), idempotently. Empty by
// default — "a deployment without it has no organiser, which is a visible,
// correct state". Application never reads configuration itself: WI-9 binds
// this record from Soarscore:Auth:BootstrapOrganisers in Composition and
// injects it like any other dependency, so the handler's constructor tells
// the whole dependency story.

namespace Soarscore.Application.Auth;

public sealed record AuthBootstrap(IReadOnlyList<string> OrganiserEmails);
