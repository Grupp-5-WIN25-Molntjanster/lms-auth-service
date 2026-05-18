namespace Lms.Auth.Application.DTOs;

public class PaginatedUserResponse
{
    public IEnumerable<UserResponse> Users { get; set; } = Enumerable.Empty<UserResponse>();
    public int PageIndex { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}