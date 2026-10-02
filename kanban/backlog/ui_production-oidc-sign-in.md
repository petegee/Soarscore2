# Story — UI prerequisite: production OIDC sign-in that works with a real Auth0 tenant

**Status:** Backlog · **Raised:** 2026-10-02 · **Source:** `SoarscoreUI/kanban/blocked/ss_engine-prerequisites.md` item 2 + `SoarscoreUI/SOARSCORE2-HANDOVER.md` §2 · **Needs owner decision before implementation**

## What

Deployed engine enforces auth (`Soarscore:Auth:Mode=oidc`), and a real Auth0 user's first sign-in succeeds — including the bootstrap organiser's.

Verified facts (re-verify before acting):

- Both Production guards in `src/Soarscore.Api/Auth/AuthSettings.cs:72-94` are commented out (`ca59f84`); `fly.toml [env]` sets no auth mode, so deployed runs `none` (anonymous) unless a Fly secret overrides it.
- Engine reads `email`/`email_verified`/`name` from the access token wire names (`src/Soarscore.Api/Auth/HttpCurrentUser.cs:100-109`, `MapInboundClaims=false` set at `Composition.cs:379,385,397,407`); first sign-in fails `auth.signIn.emailRequired` / `emailNotVerified` without them (`src/Soarscore.Application/Commands/People/LinkSignIn.cs:105-123`).
- Auth0 custom-API access tokens do not carry those claims by default; documented route is a post-login Action — but LADR-0004 D2 says "no Auth0 Actions, rules or roles" (`docs/ladr/ladr-0004-authentication.md:36`; identity resolution per-request at `:133-139`). Mock tokens carry all three claims, so the suite (`tests/Soarscore.Acceptance.Tests/Features/SigningIn.feature:1-51`, minter `Support/TestJwt.cs:39-106`) never hits this.
- Tenant still needs an SPA client (Auth Code + PKCE, refresh rotation; callbacks/logout/origins `https://soarscore-ui.fly.dev` + `http://localhost:5174`; scopes `openid profile email offline_access`) with the API audience accepting its tokens.

## Why it matters

Go-live blocker: v1 users sign in on their own devices against the deployed engine. Local dev in `mock` mode is not blocked. Do after the config/code items above — this one needs tenant ops plus a recorded decision.

## Before starting

- Owner decision: (i) post-login Action copying claims onto the access token (amends LADR-0004 D2 — needs LADR amendment + owner approval for `docs/`), or (ii) fetch `/userinfo` at `/link-sign-in` with the caller token (no Action, one outbound call per sign-in; verify tenant allows it), or (iii) anything meeting acceptance. Recommended: (ii) to preserve D2 — likely a new `IUserInfoProvider` port in Application + HttpClient impl in Api/Infrastructure (Application must not do HTTP directly).
- Read `kanban/completed/authentication-and-authorisation.md` (D2, D4, WI-10 totality), `kanban/in-progress/secure-automatic-identity-linking.md` (in flight — coordinate LinkSignIn guard changes), `docs/ladr/ladr-0004-authentication.md`, and `CompositionGuardTests` (Production guard expectations).
- Configure production `Soarscore:Auth:Domain`, `Audience`, `BootstrapOrganisers` as Fly secrets/env; plan short access-token lifetime (SPA refresh rotation covers 1–2 day events).
- House rule 2 cross-check: trust model (CLAUDE.md), `docs/users.md`; surface any new identity concept before it reaches the glossary.

## Acceptance

- [ ] Anonymous calls to the deployed engine get `401`.
- [ ] A brand-new Auth0 user's first `POST /link-sign-in` from SoarscoreUI returns a `PersonId`.
- [ ] The bootstrap organiser lands as `Organiser` (`GET /who-am-i`).
