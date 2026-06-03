using ConexaoSolidaria.Campaigns.Api.Responses;

namespace ConexaoSolidaria.Tests;

public sealed class PaginatedResponseTests
{
    [Fact]
    public void Create_ShouldCalculatePaginationMetadata()
    {
        var items = new[] { "campanha-3", "campanha-4" };

        var response = PaginatedResponse<string>.Create(items, page: 2, pageSize: 2, totalItems: 5);

        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(5, response.TotalItems);
        Assert.Equal(3, response.TotalPages);
        Assert.True(response.HasPreviousPage);
        Assert.True(response.HasNextPage);
        Assert.Equal(items, response.Items);
    }

    [Fact]
    public void Create_ShouldReturnZeroPagesWhenThereAreNoItems()
    {
        var response = PaginatedResponse<string>.Create([], page: 1, pageSize: 10, totalItems: 0);

        Assert.Equal(0, response.TotalPages);
        Assert.False(response.HasPreviousPage);
        Assert.False(response.HasNextPage);
    }
}
