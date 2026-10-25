namespace TenantPortal.Application.DTOs.Common
{
    /// <summary>
    /// Generic envelope for GET endpoints that return a collection (lists, paginated search
    /// results). Success and failure always come back with this same shape — check
    /// IsSuccess, never branch on structure.
    /// </summary>
    public sealed class ApiListResponse<T>
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public List<T>? Items { get; set; }
        public int Count { get; set; }

        // Populated for paginated list endpoints; left null for a plain
        // all-in-one-page list (e.g. a dropdown) that has nothing to page.
        public int? Page { get; set; }
        public int? PageSize { get; set; }

        public static ApiListResponse<T> Success(List<T> items, int count, int? page = null, int? pageSize = null, string? message = null) => new()
        {
            IsSuccess = true,
            Items = items,
            Count = count,
            Page = page,
            PageSize = pageSize,
            Message = message
        };

        public static ApiListResponse<T> Failure(string message) => new()
        {
            IsSuccess = false,
            Message = message
        };
    }

    /// <summary>
    /// Generic envelope for endpoints that return a single object — GET by id, or a
    /// POST/PUT that hands back the created/updated resource (e.g. the new TenantId).
    /// </summary>
    public sealed class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static ApiResponse<T> Success(T data, string? message = null) => new()
        {
            IsSuccess = true,
            Data = data,
            Message = message
        };

        public static ApiResponse<T> Failure(string message) => new()
        {
            IsSuccess = false,
            Message = message
        };
    }

    /// <summary>
    /// Generic envelope for mutation endpoints with nothing to return beyond pass/fail
    /// (e.g. DELETE, or a PUT that doesn't hand back a resource).
    /// </summary>
    public sealed class ApiResult
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }

        public static ApiResult Success(string? message = null) => new() { IsSuccess = true, Message = message };
        public static ApiResult Failure(string message) => new() { IsSuccess = false, Message = message };
    }
}
