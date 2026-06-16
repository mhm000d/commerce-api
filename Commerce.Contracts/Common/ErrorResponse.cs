namespace Commerce.Contracts.Common;

public record ErrorResponse(
    string Code,
    string Message,
    object? Details = null
);