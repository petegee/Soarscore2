// kanban/completed/command-side-steel-thread-plan.md WI-3. Every event carries an
// `At`, so handlers need an injectable clock rather than calling
// DateTimeOffset.UtcNow directly, or their tests could never be deterministic.

namespace Soarscore.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
