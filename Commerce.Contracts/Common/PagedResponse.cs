namespace Commerce.Contracts.Common;

public record PaginationMeta(
    int  Page,
    int  PageSize,
    int  TotalItems,
    int  TotalPages,
    bool HasNext,
    bool HasPrevious);

public record PagedResponse<T>(
    List<T>        Data,
    PaginationMeta Pagination);