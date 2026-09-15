// Hand-written fake (LADR-0003 "Doubles") for the WI-5 policy and pipeline
// truth-table tests — the real ICurrentUser implementations are the Api
// layer's (HttpCurrentUser, WI-9), and what every policy reads is the port.
// The fake pins the principal shapes the truth tables walk: anonymous,
// authenticated-unlinked, competitor, organiser (and any allow-listed person,
// which is a competitor whose PersonId the competition's allow-list names —
// roles beyond the list are never read by the capture policy).

using Soarscore.Application.Auth;
using Soarscore.Domain.People;

namespace Soarscore.Application.Tests.Auth;

internal sealed record FakeCurrentUser(
    bool IsAuthenticated = false,
    PersonId? PersonId = null,
    PersonRole[]? HeldRoles = null,
    string? Provider = null,
    string? Subject = null,
    string? Email = null) : ICurrentUser
{
    // The port's property type is IReadOnlyList — the positional array is the
    // fake's convenience, projected once here.
    public IReadOnlyList<PersonRole> Roles => HeldRoles ?? [];

    public bool HasRole(PersonRole role) => HeldRoles?.Contains(role) ?? false;
}
