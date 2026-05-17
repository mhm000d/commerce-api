namespace Commerce.Application.Exceptions;

public class AppException(string message, string code, int statusCode, object? details = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public object? Details { get; } = details;
}

public class NotFoundException(string message, string code)
    : AppException(message, code, 404);

// details = field-level errors dict from FluentValidation OR a custom object
public class ValidationException(string message, string code, object? details = null)
    : AppException(message, code, 400, details);

public class ForbiddenException(string message, string code)
    : AppException(message, code, 403);

public class ConflictException(string message, string code, object? details = null)
    : AppException(message, code, 409, details);

public class UnauthorizedException(string message, string code)
    : AppException(message, code, 401);
    
public class ServerException(string message, string code = "INTERNAL_ERROR")
    : AppException(message, code, 500);
    
public class EmailPermanentException(string message, Exception? inner = null)
    : Exception(message, inner);