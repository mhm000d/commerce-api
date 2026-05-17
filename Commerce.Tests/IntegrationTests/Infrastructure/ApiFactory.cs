using Commerce.Application.Database;
using Commerce.Application.Services.Admin;
using Commerce.Application.Services.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Respawn;

namespace Commerce.Tests.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    public string ConnectionString => _fixture.ConnectionString;

    public IStripeService StripeMock { get; } = Substitute.For<IStripeService>();

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
                { "Jwt:RefreshTokenExpirationDays", "7" },
                { "Frontend:BaseUrl", "http://localhost:3000" },
                { "Stripe:WebhookSecret", "whsec_test_secret" },
                { "RateLimiting:Enabled", "false" },
                { "RateLimiting:AnonymousPermitLimit", "10000" },
                { "RateLimiting:AuthenticatedPermitLimit", "10000" },
                { "RateLimiting:AuthEndpointPermitLimit", "10000" },
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
                options.UseNpgsql(_fixture.ConnectionString));

            services.AddScoped<IStripeService>(_ => StripeMock);
            services.AddScoped<IAdminService, AdminService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
    
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Called at the start of every test that uses the HTTP client.
    /// Truncates all rows so tests are fully isolated from each other.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
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
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
