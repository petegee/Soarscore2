// Per-competition capture policy — authentication-and-authorisation.md WI-4
// (D10), glossary "Capture policy" (approved 2026-09-15): who may enter which
// scores for ONE competition — organisers only, any registered person, or an
// explicit allow-list of people (organisers always pass at evaluation).
// Competition-level configuration, never class data (CLAUDE.md's core
// architectural law): the core interprets the policy generically, never
// branching on who is acting beyond applying it — D10's ordered evaluation is
// the Application layer's concern, not this model's.

using Soarscore.Domain.People;

namespace Soarscore.Domain.Competitions;

public enum CapturePolicyMode { OrganisersOnly, AnyRegisteredPerson, AllowList }

/// <summary>
/// The competition's capture policy — a value object owned by the Competition
/// aggregate (soaring-domain-class-diagram.md), replaced whole by each
/// <see cref="CapturePolicyConfigured"/>; the log keeps every configuration.
/// <see cref="Capturers"/> is the allow-list the AllowList mode reads; the
/// other modes leave it empty.
/// </summary>
public sealed record CapturePolicy(CapturePolicyMode Mode, IReadOnlyList<PersonId> Capturers);
