namespace Commerce.Contracts.Account;

public record ProfileResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string Role,
    DateTimeOffset CreatedAt);