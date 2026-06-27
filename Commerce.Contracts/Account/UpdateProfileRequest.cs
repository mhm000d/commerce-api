namespace Commerce.Contracts.Account;

public record UpdateProfileRequest(
    string Name,
    string? Phone);