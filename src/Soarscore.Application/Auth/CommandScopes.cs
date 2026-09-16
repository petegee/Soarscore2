// Marker interfaces carrying the coordinates the per-command policies need —
// authentication-and-authorisation.md §Per-command policy table. Implemented
// explicitly by the existing command and query records (no property renames):
// where a record's own coordinate already has the marker's name (OpenEntry's
// CompetitionRef, the entry commands' EntryRef) the interface is satisfied
// implicitly; where it differs (the person messages' Id) a one-line explicit
// implementation bridges the vocabulary. SelfOrOrganiserPolicy reads
// ISelfPersonCommand; CapturePolicyPolicy reads the other two (D10).

using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;

namespace Soarscore.Application.Auth;

/// <summary>A command or query addressed at one person's own record — self = the acting PersonId equals <see cref="PersonRef"/>.</summary>
public interface ISelfPersonCommand
{
    PersonId PersonRef { get; }
}

/// <summary>A command scoped to one competition by its aggregate id.</summary>
public interface ICompetitionScopedCommand
{
    CompetitionId CompetitionRef { get; }
}

/// <summary>A command scoped to one entry by its stream id; the competition comes from the entry itself.</summary>
public interface IEntryScopedCommand
{
    EntryId EntryRef { get; }
}
