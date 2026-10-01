# Story — UI prerequisite: `POST /link-sign-in` accepts an empty body

**Status:** Backlog · **Raised:** 2026-10-02 · **Source:** `SoarscoreUI/kanban/blocked/ss_engine-prerequisites.md` item 5 + `SoarscoreUI/SOARSCORE2-HANDOVER.md` §5

## What

Make behaviour and docs agree on whether `/link-sign-in` takes a body.

Verified facts (re-verify before acting):

- Route `app.MapCommand<LinkSignIn, LinkSignInResult>("/link-sign-in")` with comment "takes no body at all … identity comes from validated token" (`src/Soarscore.Api/Commands/Commands.cs:22-26`); command is bodiless `record LinkSignIn : ICommand<LinkSignInResult>` (`src/Soarscore.Application/Commands/People/LinkSignIn.cs:48-53`).
- Guide says "No body" (`src/Soarscore.Api/wwwroot/integrators-guide.html:314-318`, semantics `:674-691`), but empty body 400s ("Implicit body inferred … no body was provided"); `{}` returns `200 {"personId":{…}}`.
- UI workaround in place: sends `{}`. Blocks nothing.

## Why it matters

Trivial contract papercut on the very first call every user makes. Fix it while in the area so the guide, comment, OpenAPI, and behaviour agree.

## Before starting

- Decide: tolerate missing body (nullable command defaulting to `new LinkSignIn()`) or require `{}` and fix comment + guide. Either satisfies acceptance; tolerating both is friendliest.
- Check `MapCommand` binding path in `EndpointRouteBuilderExtensions.cs` and `HandlerRegistrationTests` / `RouteShapeTests` impact (must stay POST).
- House rule 2: no rule-corpus engagement. Guide edit needs owner awareness (house rule 4 covers `/docs`; treat `wwwroot` guide text the same way).

## Acceptance

- [ ] Behaviour and docs agree (empty body accepted, or `{}` documented as required).
