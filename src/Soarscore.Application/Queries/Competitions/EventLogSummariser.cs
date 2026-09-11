// The event-log read surface's two pure helpers — kanban/in-progress
// /competition-event-log-endpoint.md WI-1.
//
// EventName resolves the $kind discriminator every event union already
// declares ([JsonDerivedType] on the four union bases) — the same strings the
// stores use as on-disk aliases (SoarscoreEventTypes.cs's registry tag ==
// JsonDerivedType alias convention). Reflection over those attributes rather
// than a hand-written name table: the contract is the single declaration, so
// a new event type is named here the moment it is authored, with nothing to
// forget.
//
// EventLogSummarise is per-event-kind branching, never class branching — the
// class-specific values a summary cites (task codes, metric names) come from
// event payload data generically, so the core architectural law holds. The
// switch is total over the event contracts and deliberately ends with a
// null-for-unknown fallback: a future event type must degrade to name-only
// (NFR-2's additive-only line) rather than break this read surface, which is
// why the property test pins the fallback, not exhaustiveness.

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Queries.Competitions;

public static class EventName
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    /// <summary>
    /// The $kind discriminator for one event — declared once on the event
    /// union beside the contract, resolved here by reflection and cached per
    /// concrete type. Throws when a concrete event type somehow escapes its
    /// union's [JsonDerivedType] list: that is a definition-time defect and
    /// naming it as one is correct.
    /// </summary>
    public static string Of(IDomainEvent @event) =>
        Cache.GetOrAdd(@event.GetType(), type =>
        {
            for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                var derived = baseType.GetCustomAttributes<JsonDerivedTypeAttribute>()
                    .FirstOrDefault(a => a.DerivedType == type);
                if (derived?.TypeDiscriminator is string alias)
                {
                    return alias;
                }
            }

            throw new ArgumentException($"Event type {type.Name} carries no [JsonDerivedType] discriminator on any union base.");
        });
}

/// <summary>
/// The display vocabulary the summariser renders against — who a CompetitorId
/// is, resolved once by the handler from the competition fold plus the people
/// read model. A missing person degrades to the competitor number, so the log
/// never renders a bare id where a name was available.
/// </summary>
public sealed class EventLogNames(IReadOnlyDictionary<CompetitorId, string> competitors)
{
    public string Competitor(CompetitorId competitorRef) =>
        competitors.GetValueOrDefault(competitorRef, $"competitor {competitorRef.Value}");
}

public static class EventLogSummariser
{
    /// <summary>
    /// One short human-readable line per event, or null where a summary adds
    /// nothing beyond the event name. Unknown event kinds summarise to null —
    /// name-only, the graceful-degradation rule.
    /// </summary>
    public static string? Summarise(IDomainEvent @event, EventLogNames names) => @event switch
    {
        // Competition stream — setup.
        CompetitionCreated e => $"Created \"{e.Name}\" at {e.Location}, {e.StartDate:yyyy-MM-dd} to {e.EndDate:yyyy-MM-dd}",
        CompetitorRegistered e => $"{names.Competitor(e.Competitor.Id)} registered into the field",
        CompetitorWithdrawn e => $"{names.Competitor(e.CompetitorRef)} withdrawn from the field",
        PhaseDrawn e => $"Phase {e.PhaseOrdinal} drawn: {e.Rounds.Length} round(s)"
            + (e.PrescribedBy is null ? "" : $", prescribed by {e.PrescribedBy}")
            + (e.Warnings is { Count: > 0 } ? $", {e.Warnings.Count} warning(s) recorded" : ""),
        DrawAccepted e => $"Phase {e.PhaseOrdinal} draw accepted",
        DrawRejected e => $"Phase {e.PhaseOrdinal} draw rejected: {e.Reason}",
        ParameterBound e => $"Parameter \"{e.Binding.ParameterName}\" bound to {Value(e.Binding.BoundValue)} by {e.Binding.By}"
            + (e.Binding.PhaseOrdinal is null ? "" : $" (phase {e.Binding.PhaseOrdinal}, round {e.Binding.RoundOrdinal})"),
        InstrumentsDeclared e => $"{e.Declaration.Instruments.Length} instrument(s) declared by {e.Declaration.By}",
        InstrumentDeclarationCorrected e => $"Instrument declaration corrected by {e.Correction.By}: {e.Correction.Reason}",
        RulesAmended => "Rules amended retroactively",

        // Competition stream — task-round lifecycle.
        TaskRoundCompleted e => $"Task-round {e.PhaseOrdinal}.{e.RoundOrdinal}.{e.TaskRoundOrdinal} completed",
        TaskRoundAnnulled e => $"Task-round {e.PhaseOrdinal}.{e.RoundOrdinal}.{e.TaskRoundOrdinal} annulled: {e.Reason}",
        TaskRoundReopened e => $"Task-round {e.PhaseOrdinal}.{e.RoundOrdinal}.{e.TaskRoundOrdinal} reopened: {e.Reason}",
        Finalised e => $"{e.Finalisation.Scope} finalised (revision {e.Finalisation.Revision}) by {e.Finalisation.By}",

        // Competition stream — field and group operations.
        GroupSpotsAssigned e => $"Field spots assigned for task-round {e.PhaseOrdinal}.{e.RoundOrdinal}.{e.TaskRoundOrdinal} ({e.Spots.Length} assigned)",
        ReflightGroupAppended e => $"Reflight group appended to task-round {e.PhaseOrdinal}.{e.RoundOrdinal}.{e.TaskRoundOrdinal}: {e.Reason}",
        ReflightRulingRecorded e => $"Reflight ruling for {names.Competitor(e.Ruling.CompetitorRef)} in task-round {e.Ruling.TaskRound.PhaseOrdinal}.{e.Ruling.TaskRound.RoundOrdinal}.{e.Ruling.TaskRound.TaskRoundOrdinal}: {e.Ruling.Selection} — {e.Ruling.Reason}",
        TieBreakOutcomeRecorded e => $"Tie-break outcome recorded ({e.Outcome.Directive}): {e.Outcome.Reason}",
        Soarscore.Domain.Competitions.PenaltyRecorded e => $"Penalty recorded ({e.Penalty.Scope})"
            + (e.Penalty.CompetitorRef is { } against ? $" against {names.Competitor(against)}" : "")
            + $": {e.Penalty.InfractionType}",

        // Competition stream — teams.
        ScoringTeamDefined e => $"Scoring team \"{e.Team.Name}\" defined",
        ScoringTeamMembershipAssigned e => $"{names.Competitor(e.Membership.CompetitorRef)} assigned to scoring team {e.Membership.TeamRef.Value}"
            + (e.Membership.Contributes ? "" : " (non-contributing)"),
        ScoringTeamMembershipCleared e => $"Scoring-team membership cleared for {names.Competitor(e.CompetitorRef)}",
        ProtectionGroupDefined e => $"Protection group \"{e.Group.Name}\" defined",
        ProtectionGroupMemberAdded e => $"{names.Competitor(e.Membership.CompetitorRef)} added to protection group {e.Membership.GroupRef.Value}",
        ProtectionGroupMemberRemoved e => $"{names.Competitor(e.CompetitorRef)} removed from protection group {e.GroupRef.Value}",
        TeamClassificationConfigured e => $"Team classification {(e.Configuration.Enabled ? "enabled" : "disabled")} (method: {e.Configuration.Method})",

        // Entry streams.
        EntryOpened e => $"{names.Competitor(e.CompetitorRef)} entered task-round {e.PhaseOrdinal}.{e.RoundOrdinal}.{e.TaskRoundOrdinal}"
            + (e.CountsForRoundOrdinal is { } countsFor ? $" (counts for round {countsFor}: {e.Reason})" : ""),
        FlightOpened e => $"Flight {e.Sequence} opened",
        MeasurementCaptured e => $"Flight {e.FlightSequence}: {e.Measurement.Metric} = {Value(e.Measurement.Value)}"
            + (e.Measurement.Instrument is { } instrument ? $" (on {instrument})" : ""),
        MeasurementAmended e => $"Flight {e.FlightSequence}: {e.Metric} amended to {Value(e.Amendment.NewValue)} — {e.Amendment.Reason}",
        EntryAnnulled e => $"Entry annulled by {e.Annulment.By}: {e.Annulment.Reason}",
        Soarscore.Domain.Entries.PenaltyRecorded e => $"Penalty recorded ({e.Penalty.Scope}): {e.Penalty.InfractionType}",

        // Unknown kinds summarise to nothing — name-only, never an error.
        _ => null,
    };

    private static string Value(MeasuredValue value) => value.Kind switch
    {
        MeasuredKind.Number => value.Number?.ToString(CultureInfo.InvariantCulture) ?? "?",
        MeasuredKind.Flag => value.Flag?.ToString(CultureInfo.InvariantCulture) ?? "?",
        _ => "?",
    };
}