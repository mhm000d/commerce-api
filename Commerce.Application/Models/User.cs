namespace Commerce.Application.Models;

public class User
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; private set; } = [];
    public ICollection<Rating> Ratings { get; private set; } = [];

    // ── Factory ───────────────────────────────────────────────────────────────
    public static User Create(string name, string email, string rawPassword, string? phone = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword),
            Phone = phone,
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    public bool VerifyPassword(string rawPassword)
        => BCrypt.Net.BCrypt.Verify(rawPassword, PasswordHash);
    
    public void UpdatePassword(string rawPassword)
    {
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);
        // Caller still responsible for revoking RefreshTokens
    }

    public void PromoteToAdmin() => Role = UserRole.Admin;

    public void UpdateProfile(string name, string? phone)
    {
        Name = name;
        Phone = phone;
    }
}

public enum UserRole
{
    Admin,
    Customer
}