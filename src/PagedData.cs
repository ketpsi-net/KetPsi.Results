namespace KetPsi.Results;

/// <summary>
/// Acts as a data envelope for paginated datasets, separating dataset metadata
/// from operational status. Intended to be returned inside a <see cref="Result{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the items in the dataset.</typeparam>
public record PagedData<T>
{
    /// <summary>Gets the slice of data for the current page.</summary>
    public IReadOnlyList<T> Items { get; init; }

    /// <summary>Gets the current 1-based page number.</summary>
    public int PageNumber { get; init; }

    /// <summary>Gets the maximum number of items requested per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Gets the total number of items available across all pages.</summary>
    public long TotalCount { get; init; }

    /// <summary>Gets a value indicating whether there is a subsequent page of data available.</summary>
    public bool HasNextPage => PageNumber * PageSize < TotalCount;

    /// <summary>Gets a value indicating whether there is a preceding page of data available.</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PagedData{T}"/> record.
    /// </summary>
    internal PagedData(IReadOnlyList<T> items, int pageNumber, int pageSize, long totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Creates a new paginated data envelope.
    /// </summary>
    /// <param name="items">The slice of data for the current page.</param>
    /// <param name="pageNumber">The current 1-based page number.</param>
    /// <param name="pageSize">The maximum number of items requested per page.</param>
    /// <param name="totalCount">The total number of items available across all pages.</param>
    /// <returns>A newly created <see cref="PagedData{T}"/>.</returns>
    public static PagedData<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, long totalCount)
        => new(items, pageNumber, pageSize, totalCount);
}