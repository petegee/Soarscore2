using Soarscore.Api;

Composition.Build(args).Run();

// kanban/completed/capture-a-score-steel-thread-plan.md WI-13: the marker
// WebApplicationFactory<Program> needs to host this app in-process for the
// Reqnroll acceptance suite. Top-level statements already generate an
// internal `partial class Program`; this declaration only widens its
// visibility to `public` so Soarscore.Acceptance.Tests can name it — nothing
// about startup behaviour changes.
public partial class Program;
