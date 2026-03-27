using Commerce.Application.Database;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Commerce.Tests.IntegrationTests.Infrastructure;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("dhi.io/postgres:18-debian13-dev")
        .WithDatabase("commerce_test")
        .WithUsername("test")
        .WithPassword("test123")
        .Build();

    // Exposed so IntegrationTestBase can build AppDbContext instances
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        // Run your actual EF Core migrations against this real database.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Container stops and is removed after all tests finish
        await _container.DisposeAsync();
    }
}