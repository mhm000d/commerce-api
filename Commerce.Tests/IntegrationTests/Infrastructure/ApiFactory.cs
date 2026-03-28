using Commerce.Application.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Commerce.Tests.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = 
        new PostgreSqlBuilder("dhi.io/postgres:18-debian13-dev")
            .WithDatabase("commerce_api_test")
            .WithUsername("test")
            .WithPassword("test123")
            .Build();
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "ForTheLoveOfGodStoreAndLoadThisSecurely" },
                { "Jwt:Issuer", "CommerceApi" },
                { "Jwt:Audience", "CommerceClient" },
                { "Jwt:AccessTokenExpirationMinutes", "15" },
                { "Jwt:RefreshTokenExpirationDays", "7" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Replace with real PostgreSQL test container
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_container.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
    
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Called at the start of every test that uses the HTTP client.
    /// Truncates all rows so tests are fully isolated from each other.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });

        await respawner.ResetAsync(conn);
    }
    
    /// <summary>
    /// A direct DbContext backed by the test container.
    /// Use only in Arrange/Assert — never pass to the code under test.
    /// Caller is responsible for disposing.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }
}