using Commerce.Application.Database;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    // AppDbContext your tests use to arrange data and assert state
    protected AppDbContext DbContext { get; private set; } = null!;

    public virtual Task InitializeAsync()
    {
        try
        {
            // Fresh DbContext for every test — avoids EF Core's change tracker
            // bleeding state from one test into another.
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options;
        
            DbContext = new AppDbContext(options);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
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