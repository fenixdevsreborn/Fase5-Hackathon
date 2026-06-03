namespace ConexaoSolidaria.Campaigns.Api.Responses;

public sealed record PaginatedResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage)
{
    public static PaginatedResponse<T> Create(
        IReadOnlyCollection<T> items,
        int page,
        int pageSize,
        int totalItems)
    {
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PaginatedResponse<T>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            page > 1 && totalPages > 0,
            page < totalPages);
    }
}
