using Commerce.Application.Database;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Commerce.Tests.IntegrationTests.Infrastructure;

public class DatabaseFixture : IAsyncLifetime
{
    private static readonly string PostgresImage =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_IMAGE") ?? "postgres:18";

    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("commerce_test")
        .WithUsername("test")
        .WithPassword("test123")
        .Build();

    // Exposed so IntegrationTestBase can build AppDbContext instances
    public string ConnectionString { get; private set; } = null!;

    private bool _migrated;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (ConnectionString == null)
            {
                await _container.StartAsync();
                ConnectionString = _container.GetConnectionString();
            }

            if (!_migrated)
            {
                // Run your actual EF Core migrations against this real database.
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options;

                await using var context = new AppDbContext(options);
                await context.Database.MigrateAsync();
                _migrated = true;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        // Container stops and is removed after all tests finish
        await _container.DisposeAsync();
    }
}
