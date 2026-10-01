namespace AtlasSupply.Application;

public sealed record PageRequest(int PageIndex, int PageSize)
{
    public void Validate()
    {
        if (PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PageIndex), "Page index cannot be negative.");
        }

        if (PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(PageSize), "Page size must be between 1 and 100.");
        }

        if (PageIndex > int.MaxValue / PageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(PageIndex), "Page index and page size exceed the supported paging range.");
        }
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
