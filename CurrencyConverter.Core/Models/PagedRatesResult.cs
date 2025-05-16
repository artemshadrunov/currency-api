namespace CurrencyConverter.Core.Models;

public class PagedRatesResult
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    public Dictionary<DateTime, decimal> Rates { get; set; } = new();
}