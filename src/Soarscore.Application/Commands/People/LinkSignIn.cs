// authentication-and-authorisation.md WI-7 (D5). Sign-in is get-or-create,
// resolved in three arms: identity link first (the common path — this token
// has signed in before), then the token's email (link the identity to the
// person already registered under it), then create from the token claims.
// No body fields: the identity comes from ICurrentUser, never from request
// JSON — a caller can spoof a body, never the validated token.
//
// The (provider, subject) unique index in the people projection — not the
// pre-reads — is the arbiter, exactly the RegisterPerson email precedent
// (RegisterPerson.cs): the pre-reads are best-effort for the common path, the
// adapter translates the index violation into eventStore.uniqueConstraintViolation,
// and this handler re-runs the resolution EXACTLY ONCE on that code before
// propagating (D5's bounded retry — a concurrent sign-in has committed the
// link this one raced; the retry resolves to the winner instead of creating
// a duplicate). A second violation propagates: the retry is resolution, not
// enforcement (LADR-0001 §4.4's read-check-write ban is untouched — the
// unique index still decides).
//
// D3 bootstrap rides every arm: a config-listed email whose person lacks
// Organiser gains it. The handler folds the person's stream before deciding,
// so GrantRole's person.roleAlreadyHeld refusal cannot fire — the folded
// state IS the idempotence check. On the create arm there is no stream yet,
// so the grant rides the same NoStream append as the registration.

using Soarscore.Application.Auth;
using Soarscore.Application.Queries.People;
using Soarscore.Application.Shared.People;
using Soarscore.Domain;
using Soarscore.Domain.People;

namespace Soarscore.Application.Commands.People;

// No body fields — identity comes from ICurrentUser, never from request JSON.
public sealed record LinkSignIn : ICommand<LinkSignInResult>;

public sealed record LinkSignInResult(PersonId PersonId, bool PersonCreated);

public sealed class LinkSignInHandler(
    IEventStore eventStore,
    IPeopleQuery peopleQuery,
    ICurrentUser currentUser,
    IClock clock,
    AuthBootstrap bootstrap) : ICommandHandler<LinkSignIn, LinkSignInResult>
{
    public async Task<Result<LinkSignInResult>> HandleAsync(LinkSignIn command, CancellationToken cancellationToken)
    {
        var result = await SignInOnceAsync(cancellationToken);

        // D5: the pre-reads are best-effort for the common path; the unique
        // index is the arbiter. A violation means a concurrent sign-in won
        // the race — re-run the resolution once and take whichever arm the
        // world now offers; a second violation propagates.
        if (result.IsFailure && result.Code == "eventStore.uniqueConstraintViolation")
        {
            result = await SignInOnceAsync(cancellationToken);
        }

        return result;
    }

    private async Task<Result<LinkSignInResult>> SignInOnceAsync(CancellationToken cancellationToken)
    {
        // Captured once: nullable reference annotations are compile-time only,
        // and the null-flow the email guard establishes below does not carry
        // across property reads (ValidateContact's reasoning in reverse).
        var email = currentUser.Email;
        var name = currentUser.Name;

        // Arm 1 — identity link found: that person. Any validated token may
        // reach this handler (Authenticated policy), so the bootstrap check
        // runs here too: a listed email means this sign-in is the organiser
        // the deployment named, whatever path produced the person.
        if (currentUser.Provider is { } provider && currentUser.Subject is { } subject)
        {
            var identity = await peopleQuery.FindIdentityAsync(provider, subject, cancellationToken);
            if (identity is { } match)
            {
                var granted = await ApplyBootstrapGrantAsync(match.PersonId, cancellationToken);
                return granted.IsSuccess
                    ? Result<LinkSignInResult>.Success(new LinkSignInResult(match.PersonId, false))
                    : Result<LinkSignInResult>.Failure(granted.Code!, granted.Message!, granted.Defects);
            }
        }

        // Arms 2 and 3 both need the token's email — to match on, and to
        // register from. No email claim ⇒ neither can run (D5's create arm
        // names the failure).
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<LinkSignInResult>.Failure(
                "auth.signIn.emailRequired",
                "Sign-in requires an email claim — it is how the identity is matched to, or creates, a person.");
        }

        // Arm 2 — a person already registered under this email: link the
        // identity to that person's stream.
        var existing = await peopleQuery.FindByEmailAsync(email, cancellationToken);
        if (existing is { } person)
        {
            return await LinkToExistingPersonAsync(
                person.Id, currentUser.Provider, currentUser.Subject, cancellationToken);
        }

        // Arm 3 — nobody to match: create the person from the token claims
        // and link the identity in the same NoStream append.
        return await CreatePersonAsync(email, name, cancellationToken);
    }

    // Arm 2: the email-matched person's stream is loaded (fold + version),
    // the identity is decided onto it, appended Exact — then the bootstrap
    // check runs against the freshly linked person, per the WI-7 flow order.
    private async Task<Result<LinkSignInResult>> LinkToExistingPersonAsync(
        PersonId personId, string? provider, string? subject, CancellationToken cancellationToken)
    {
        var loaded = await PersonLoader.LoadAsync(eventStore, personId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<LinkSignInResult>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (folded, version) = loaded.Value;
        var link = folded.LinkIdentity(provider!, subject!, clock.UtcNow);
        if (link.IsFailure)
        {
            return Result<LinkSignInResult>.Failure(link.Code!, link.Message!, link.Defects);
        }

        var append = await eventStore.AppendAsync(
            personId.Value, ExpectedVersion.Exact(version), [link.Value], cancellationToken);
        if (append.IsFailure)
        {
            return Result<LinkSignInResult>.Failure(append.Code!, append.Message!, append.Defects);
        }

        var granted = await ApplyBootstrapGrantAsync(personId, cancellationToken);
        return granted.IsSuccess
            ? Result<LinkSignInResult>.Success(new LinkSignInResult(personId, false))
            : Result<LinkSignInResult>.Failure(granted.Code!, granted.Message!, granted.Defects);
    }

    // Arm 3: register + link (+ bootstrap grant) decided against the token's
    // claims, appended as ONE NoStream call — the person and its first
    // identity link are born together, so there is no window where the
    // account exists but cannot be signed into.
    private async Task<Result<LinkSignInResult>> CreatePersonAsync(
        string email, string? name, CancellationToken cancellationToken)
    {
        var id = PersonId.New();

        // Name from the token's name claim, defaulting to the email
        // local-part (WI-7).
        var displayName = !string.IsNullOrWhiteSpace(name)
            ? name
            : email.Split('@')[0];

        var registered = Person.Register(id, displayName, new ContactDetails { Email = email }, null, clock.UtcNow);
        if (registered.IsFailure)
        {
            return Result<LinkSignInResult>.Failure(registered.Code!, registered.Message!, registered.Defects);
        }

        var person = Person.Apply(null, registered.Value)!;
        var link = person.LinkIdentity(currentUser.Provider!, currentUser.Subject!, clock.UtcNow);
        if (link.IsFailure)
        {
            return Result<LinkSignInResult>.Failure(link.Code!, link.Message!, link.Defects);
        }

        var events = new List<IDomainEvent> { registered.Value, link.Value };

        // D3 bootstrap on the create arm: a fresh person holds no roles, so
        // the decide's duplicate refusal cannot fire — the fold-equivalent
        // check is the empty Roles set this person was just born with.
        if (IsBootstrapEmail(email))
        {
            var grant = person.GrantRole(PersonRole.Organiser, clock.UtcNow);
            if (grant.IsFailure)
            {
                return Result<LinkSignInResult>.Failure(grant.Code!, grant.Message!, grant.Defects);
            }

            events.Add(grant.Value);
        }

        var append = await eventStore.AppendAsync(id.Value, ExpectedVersion.NoStream, events, cancellationToken);
        return append.IsFailure
            ? Result<LinkSignInResult>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<LinkSignInResult>.Success(new LinkSignInResult(id, true));
    }

    // The D3 bootstrap step for arms 1 and 2 — the person's stream already
    // exists, so the grant is its own Exact append after a fresh fold: the
    // folded state is the check, and the decide's duplicate refusal
    // (person.roleAlreadyHeld) cannot fire. A listed email whose person
    // already holds Organiser falls straight through — idempotent by
    // construction, which is what makes re-signing-in safe to repeat.
    private async Task<Result<PersonId>> ApplyBootstrapGrantAsync(PersonId personId, CancellationToken cancellationToken)
    {
        if (!IsBootstrapEmail(currentUser.Email))
        {
            return Result<PersonId>.Success(personId);
        }

        var loaded = await PersonLoader.LoadAsync(eventStore, personId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PersonId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (person, version) = loaded.Value;
        if (person.Roles.Contains(PersonRole.Organiser))
        {
            return Result<PersonId>.Success(personId);
        }

        var grant = person.GrantRole(PersonRole.Organiser, clock.UtcNow);
        if (grant.IsFailure)
        {
            return Result<PersonId>.Failure(grant.Code!, grant.Message!, grant.Defects);
        }

        var append = await eventStore.AppendAsync(
            personId.Value, ExpectedVersion.Exact(version), [grant.Value], cancellationToken);
        return append.IsFailure
            ? Result<PersonId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<PersonId>.Success(personId);
    }

    // D3: the list contains email addresses; the comparison is
    // ordinal-ignore-case because emails differ in case across providers.
    private bool IsBootstrapEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && bootstrap.OrganiserEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
}