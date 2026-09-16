// authentication-and-authorisation.md WI-7 (D9). The one read that cannot be
// reconstructed from the JWT alone: the SPA/BFF cannot derive the Soarscore
// PersonId from the token (D2), and needs it — plus the roles that never live
// in tokens — to decide what to render. The handler does its own identity
// lookup rather than trusting user.PersonId/user.Roles, so the view is right
// whatever the WI-9 middleware populated (and under none-mode, where it
// populates nothing).
//
// Shapes (WI-7's test contract):
//   unauthenticated            → (false, null, [], null) — no token, no name.
//   authenticated + linked     → (true, personId, person's roles, person's
//                                name from the read model — the token's name
//                                claim as the fallback if the summary has not
//                                landed yet).
//   authenticated + unlinked   → (true, null, [], token name) — nobody yet,
//                                but the token still says who is asking.

using Soarscore.Application.Auth;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Queries.People;

public sealed record WhoAmI : IQuery<CurrentUserView>;

public sealed record CurrentUserView(
    bool IsAuthenticated,
    PersonId? PersonId,
    IReadOnlyList<PersonRole> Roles,
    string? Name);

public sealed class WhoAmIHandler(ICurrentUser currentUser, IPeopleQuery peopleQuery) : IQueryHandler<WhoAmI, CurrentUserView>
{
    public async Task<Result<CurrentUserView>> HandleAsync(WhoAmI query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result<CurrentUserView>.Success(new CurrentUserView(false, null, [], null));
        }

        // D2 — identity resolution is a per-request read-model lookup; roles
        // never live in tokens.
        if (currentUser.Provider is { } provider && currentUser.Subject is { } subject)
        {
            var identity = await peopleQuery.FindIdentityAsync(provider, subject, cancellationToken);
            if (identity is { } match)
            {
                var summaries = await peopleQuery.FindByIdsAsync([match.PersonId], cancellationToken);
                return Result<CurrentUserView>.Success(new CurrentUserView(
                    true, match.PersonId, match.Roles, summaries.Count > 0 ? summaries[0].Name : currentUser.Name));
            }
        }

        // Authenticated at the IdP but linked to no person: an unlinked
        // identity is nobody, so no PersonId and no roles — only the token's
        // own name claim.
        return Result<CurrentUserView>.Success(new CurrentUserView(true, null, [], currentUser.Name));
    }
}