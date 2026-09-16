# Story — Token exchange federation (RFC 8693) for integrators' own IdPs

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, decision D12)

## What

Support integrators whose users authenticate against the integrator's *own*
identity provider: a token exchange (RFC 8693) at the Auth0 tenant converts
the integrator's token into one of ours, and Soarscore sees an ordinary
principal. D12 settled the machine-actor path (client credentials, no
delegation, `sub` without `|` = the client itself) and explicitly stubbed
federation as future work, not designed here.

## Why it matters

The client-credentials recipe makes Soarscore the actor-granting authority
and requires every integrator to register a client on our tenant. Larger
integrators (a national scoring platform, say) will want their own user
identity carried through, and re-registering every one of their users as
Soarscore persons is the wrong shape. Federation keeps Auth0 the single
token-validating boundary (D2's "Auth0 is a pure identity provider" stays
true; the Api's JwtBearer config does not change).

## Before starting

- Read D12 of `kanban/completed/authentication-and-authorisation.md` (the
  rejected scope-in-token stance binds here too — an exchanged token's
  authority still resolves from Soarscore's read model, never from claims).
- Confirm Auth0's RFC 8693 support shape on the tenant tier in use
  (custom-token exchange requires an add-on/entitlement — pricing is a real
  constraint to surface before design).
- Decide the identity mapping policy: what `provider` value an exchanged
  identity carries (the `provider|subject` parse rule in the current-user
  middleware must classify it), and whether exchanged users pre-provision
  via `BindIdentity` like M2M clients.
- Check LADR-0004: this is tenant configuration, not core code — the story
  may be a doc/config story, not a code story. Do not add a second
  token-validating path in the Api.
- House rule 2 cross-check: the trust model in CLAUDE.md (who can act);
  no rulebook engagement.
