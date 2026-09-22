// The per-command policy table — authentication-and-authorisation.md
// §Per-command policy table, "the literal, hand-written Dictionary<Type,
// ICommandPolicy> from the §Per-command policy table": one explicit
// registration per message, no assembly scanning (house style — the mapping
// is inspectable by reading it). Policy kinds: A = Authenticated (queries by
// default, D4), O = Organiser, S = self-or-organiser, C = capture policy
// (D10).
//
// The pipeline FAILS CLOSED: a message type absent from this table denies
// with auth.policyMissing (AuthorizationPipeline.cs). Totality — every mapped
// message having a row — is enforced by test in WI-10, which reuses
// HandlerRegistrationTests' route enumeration; this table plus the routes
// must move together.

using Soarscore.Application.Auth.Policies;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Soarscore.Application.Queries.Scoring;

namespace Soarscore.Application.Auth;

public static class CommandPolicyTable
{
    public static readonly IReadOnlyDictionary<Type, ICommandPolicy> Table = new Dictionary<Type, ICommandPolicy>
    {
        // ---- People (commands) -------------------------------------------------
        [typeof(RegisterPerson)] = new OrganiserPolicy(),          // manual pre-registration; self-service is LinkSignIn (WI-7)
        [typeof(RenamePerson)] = new SelfOrOrganiserPolicy(),
        // secure-automatic-identity-linking.md (security review 2026-09-17):
        // the S kind plus the email-ownership guard — the contact email is
        // D5's email-match key and D3's bootstrap key.
        [typeof(ChangePersonContactDetails)] = new ContactDetailsPolicy(),
        [typeof(ChangePersonClubAffiliation)] = new SelfOrOrganiserPolicy(),

        // ---- Authentication (WI-7) ---------------------------------------------
        [typeof(LinkSignIn)] = new AuthenticatedPolicy(),          // any validated token; the handler does the get-or-create (D5)
        [typeof(GrantRole)] = new OrganiserPolicy(),
        [typeof(RevokeRole)] = new OrganiserPolicy(),
        [typeof(ConfigureCapturePolicy)] = new OrganiserPolicy(),  // who may enter scores is organiser-set configuration (D10)
        [typeof(BindIdentity)] = new OrganiserPolicy(),            // D12: organiser binds machine/external identities, pre-provisions people

        // ---- Competition classes (shared master data) --------------------------
        [typeof(PublishClassDefinition)] = new OrganiserPolicy(),

        // ---- Competition commands (structure, rulings, CD authority) -----------
        [typeof(CreateCompetition)] = new OrganiserPolicy(),
        [typeof(RegisterCompetitor)] = new OrganiserPolicy(),      // D7: self-service entry registration is a later story
        [typeof(WithdrawCompetitor)] = new OrganiserPolicy(),      // D7: self-withdrawal ditto
        [typeof(DrawPhase)] = new OrganiserPolicy(),
        [typeof(PrescribeDraw)] = new OrganiserPolicy(),
        [typeof(AcceptDraw)] = new OrganiserPolicy(),
        [typeof(RejectDraw)] = new OrganiserPolicy(),
        [typeof(BindParameter)] = new OrganiserPolicy(),
        [typeof(DeclareInstruments)] = new OrganiserPolicy(),
        [typeof(CorrectInstrumentDeclaration)] = new OrganiserPolicy(),
        [typeof(CompleteTaskRound)] = new OrganiserPolicy(),
        [typeof(ReopenTaskRound)] = new OrganiserPolicy(),
        [typeof(AnnulTaskRound)] = new OrganiserPolicy(),
        [typeof(FinaliseCompetition)] = new OrganiserPolicy(),
        [typeof(RecordCompetitionPenalty)] = new OrganiserPolicy(),
        [typeof(AppendReflightGroup)] = new OrganiserPolicy(),
        [typeof(AssignGroupSpots)] = new OrganiserPolicy(),
        [typeof(RecordReflightRuling)] = new OrganiserPolicy(),
        [typeof(RecordTieBreakOutcome)] = new OrganiserPolicy(),

        // ---- Team and protection-group commands (competition configuration) ----
        [typeof(DefineScoringTeam)] = new OrganiserPolicy(),
        [typeof(AssignScoringTeamMembership)] = new OrganiserPolicy(),
        [typeof(ClearScoringTeamMembership)] = new OrganiserPolicy(),
        [typeof(ConfigureTeamClassification)] = new OrganiserPolicy(),
        [typeof(DefineProtectionGroup)] = new OrganiserPolicy(),
        [typeof(AddProtectionGroupMember)] = new OrganiserPolicy(),
        [typeof(RemoveProtectionGroupMember)] = new OrganiserPolicy(),

        // ---- Entry commands ----------------------------------------------------
        [typeof(OpenEntry)] = new CapturePolicyPolicy(),           // competition-scoped capture (D10)
        [typeof(OpenFlight)] = new CapturePolicyPolicy(),          // entry-scoped — the policy resolves the competition via IEntryQuery
        [typeof(CaptureMeasurement)] = new CapturePolicyPolicy(),
        [typeof(AmendMeasurement)] = new CapturePolicyPolicy(),
        [typeof(AnnulEntry)] = new OrganiserPolicy(),              // rulings, not capture
        [typeof(RecordEntryPenalty)] = new OrganiserPolicy(),      // rulings, not capture

        // ---- Queries — D4: authenticated principal by default -----------------
        [typeof(FindClassDefinitions)] = new AuthenticatedPolicy(),
        [typeof(GetClassDefinition)] = new AuthenticatedPolicy(),
        [typeof(FindCompetitions)] = new AuthenticatedPolicy(),
        [typeof(GetCompetition)] = new AuthenticatedPolicy(),
        [typeof(GetCompetitionEventLog)] = new AuthenticatedPolicy(),
        [typeof(GetDrawProtectionDiagnostics)] = new AuthenticatedPolicy(),
        [typeof(GetTeamRosters)] = new AuthenticatedPolicy(),
        [typeof(FindEntries)] = new AuthenticatedPolicy(),
        // Security review 2026-09-16, D4 deviation: the roster carries every
        // pilot's contact details and roles — organiser-only.
        [typeof(FindPeople)] = new OrganiserPolicy(),
        // Self-service for one's own record — the S kind's exact semantics,
        // same as RenamePerson; organiser for anyone's (security review
        // 2026-09-16, D4 deviation).
        [typeof(GetPerson)] = new SelfOrOrganiserPolicy(),
        [typeof(GetPendingTieBreaks)] = new AuthenticatedPolicy(),
        [typeof(ScoreCompetition)] = new AuthenticatedPolicy(),
        [typeof(ScoreTeamStandings)] = new AuthenticatedPolicy(),
        [typeof(ScoreTaskRound)] = new AuthenticatedPolicy(),
        [typeof(GetTaskRoundRecording)] = new AuthenticatedPolicy(),
        [typeof(WhoAmI)] = new AuthenticatedPolicy(),              // D9 — the identity bridge the front-end flow requires
    };
}
