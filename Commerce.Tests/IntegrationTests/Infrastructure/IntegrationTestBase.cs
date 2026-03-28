using Commerce.Application.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Commerce.Tests.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    protected AppDbContext DbContext { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });

        await respawner.ResetAsync(conn);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        DbContext = new AppDbContext(options);
    }

    public virtual async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }

    // ── Helpers available to all test classes ─────────────────────────────────

    // Saves entities to the DB in the Arrange phase.
    // Using a separate SaveChangesAsync call in Arrange is intentional —
    // it simulates the state the database would be in before your code runs.
    protected async Task SaveAsync(params object[] entities)
    {
        DbContext.AddRange(entities);
        await DbContext.SaveChangesAsync();

        // Detach everything after saving.
        // This forces the service under test to load fresh data from the DB
        // rather than getting it from EF Core's identity cache.
        foreach (var entry in DbContext.ChangeTracker.Entries().ToList())
            entry.State = EntityState.Detached;
    }
}