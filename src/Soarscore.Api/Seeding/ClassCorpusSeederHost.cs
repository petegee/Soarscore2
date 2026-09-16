// Deployment seeding, policy half — kanban/in-progress/seed-class-corpus-at-startup.md
// WI-2. ClassCorpusSeeder (Application) is the mechanics; this hosted service
// is the deployment policy around it:
//
//   - Gate: Soarscore:SeedCorpus (default true) — a deployment that manages its
//     own catalogue can turn seeding off.
//   - Directory: Soarscore:SeedCorpusDirectory, default <AppContext.BaseDirectory>/seed
//     — what the Dockerfile copies tools/Soarscore.SeedData/json to (/app/seed).
//     A missing directory logs a warning and skips, rather than failing: a bare
//     `dotnet run` from the repo has no seed directory and must still start.
//   - Scope: handlers are Scoped (Composition.cs), so the dispatcher is resolved
//     from a scope created for this pass, not the root provider.
//   - Timing: registered hosted services start before GenericWebHostService
//     (Kestrel), so seeding completes before the API accepts a request — a
//     client can never observe an empty catalogue on a fresh store. If seeding
//     throws (the corpus is test-verified, so any failure is a bug), StartAsync
//     propagates and the host fails fast.
//
// Re-seeding on every boot is the point, not a defect: the publish command is
// idempotent by content hash, so this costs sixteen no-op appends after the
// first boot and survives container restarts and store re-creation alike.

using Soarscore.Api.Auth;
using Soarscore.Application;
using Soarscore.Application.Auth;
using Soarscore.Application.Seeding;

namespace Soarscore.Api.Seeding;

public sealed class ClassCorpusSeederHost(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<ClassCorpusSeederHost> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Soarscore:SeedCorpus", true))
        {
            logger.LogInformation(
                "Class corpus seeding disabled (Soarscore:SeedCorpus) — the catalogue will only contain what is published through the API.");
            return;
        }

        var directory = configuration["Soarscore:SeedCorpusDirectory"]
                        ?? Path.Combine(AppContext.BaseDirectory, "seed");

        if (!Directory.Exists(directory))
        {
            logger.LogWarning(
                "Class corpus seeding enabled but no corpus found at {Directory} — starting with an empty class catalogue."
                + " Ship the corpus (the Docker image copies tools/Soarscore.SeedData/json to /app/seed) or set Soarscore:SeedCorpusDirectory.",
                directory);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        // System seeding is not a user request (D1/D3): the system actor is
        // bound EXPLICITLY here, not by the carrier's default — RequestCaller
        // fails closed to AnonymousCurrentUser, and the current-principal
        // middleware never runs in a seeder scope to rebind it. With the
        // system actor bound, the authorization pipeline allows this publish
        // exactly as it would for an organiser. Under none-mode no carrier is
        // registered (and no pipeline consults it), so there is nothing to bind.
        if (scope.ServiceProvider.GetService<RequestCaller>() is { } caller)
        {
            caller.User = new SystemCurrentUser();
        }
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var report = await ClassCorpusSeeder.SeedAsync(dispatcher, directory, cancellationToken);

        logger.LogInformation(
            "Class corpus seeded: {Count} definition(s) from {Directory}{Details}",
            report.Count,
            directory,
            report.Count == 0
                ? "."
                : ": " + string.Join(", ", report.Published.Select(p => $"{p.FileName} {p.ContentHash[..8]}")));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
