using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Auth;
using Commerce.Application.Services.Email;
using Commerce.Application.Validators;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Services;

public class AuthServiceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private AuthService _authService = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var userValidator = new UserValidator();
        var refreshTokenValidator = new RefreshTokenValidator();
        var passwordResetTokenValidator = new PasswordResetTokenValidator();
        var emailNotificationService = Substitute.For<IEmailNotificationService>();

        // Use a real TokenService here because token generation/hashing is part of
        // the behavior you want to test end-to-end.
        var tokenService = CreateTokenService();

        // Logger: NSubstitute gives no-op logger that satisfies
        // the constructor without polluting test output.
        var logger = Substitute.For<ILogger<AuthService>>();

        // Wire it all up with the same DbContext that IntegrationTestBase manages.
        _authService = new AuthService(
            dbContext: DbContext,
            userValidator: userValidator,
            refreshTokenValidator: refreshTokenValidator,
            passwordResetTokenValidator: passwordResetTokenValidator,
            tokenService: tokenService,
            emailService: emailNotificationService,
            logger: logger
        );
    }
    
    // ── Tests ──────────────────────────────────────────────────────────
    // ── Registration ──────────────────────────────────────────────────────────
    [Fact]
    public async Task Register_WithValidData_ShouldCreateUserAndReturnTokens()
    {
        // Act & Arrange
        var result = await _authService.RegisterAsync(
            name: "Adam Ahmed",
            email: "ahmed@example.com",
            rawPassword: "Password1",
            phone: null
        );

        // Assert the returned tokens
        result.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.RawRefreshToken.ShouldNotBeNullOrWhiteSpace();
        result.User.Email.ShouldBe("ahmed@example.com");

        // Assert the DB state
        var savedUser = await DbContext.Users
            .SingleOrDefaultAsync(u => u.Email == "ahmed@example.com");

        savedUser.ShouldNotBeNull();
        savedUser.Name.ShouldBe("Adam Ahmed");
        savedUser.PasswordHash.ShouldNotBe("Password1"); // never stored raw
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var existingUser = User.Create(
            name: "Existing User",
            email: "taken@example.com",
            rawPassword: "Password1",
            phone: null
        );
        await SaveAsync(existingUser);

        // Act
        var act = () => _authService.RegisterAsync(
            name: "New User",
            email: "taken@example.com",   // same email
            rawPassword: "Password1",
            phone: null
        );

        // Assert
        await act.ShouldThrowAsync<ConflictException>();
    }

    // ── Refresh Token Reuse Detection ─────────────────────────────────────────
    [Fact]
    public async Task Refresh_WhenRevokedTokenReused_ShouldRevokeFamilyAndThrow()
    {
        // Arrange: register once
        var register = await _authService.RegisterAsync("Ali", "ali@example.com", "Password1", null);
        var initialToken = register.RawRefreshToken;

        // Rotate once — this revokes the original token and issues a new one
        await _authService.RefreshAsync(initialToken);

        // Act: reuse the now-revoked original token
        var act = () => _authService.RefreshAsync(initialToken);

        // Assert: security exception thrown
        var ex = await act.ShouldThrowAsync<UnauthorizedException>();
        ex.Code.ShouldBe("SESSION_COMPROMISED");

        // Assert: the entire token family is revoked in the DB
        var familyTokens = await DbContext.RefreshTokens
            .AsNoTracking() // Ensure we're reading fresh from DB
            .ToListAsync();
        
        familyTokens.ShouldAllBe(t => t.IsRevoked, $"Tokens: {string.Join(", ", familyTokens.Select(t => $"{t.Id}({t.IsRevoked}) reason: {t.RevokedReason}"))}");
    }
    
    // ── Helpers ───────────────────────────────────────────────────────────────
    private static TokenService CreateTokenService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "this-is-a-test-secret-key-long-enough-for-hs256",
                ["Jwt:Issuer"] = "commerce-test",
                ["Jwt:Audience"] = "commerce-test",
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

        return new TokenService(config);
    }
}