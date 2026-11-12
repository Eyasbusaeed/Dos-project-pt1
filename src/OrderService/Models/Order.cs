namespace OrderService.Models;

/// <summary>One row of the persistent order log.</summary>
public class Order
{
    public int OrderId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    /// <summary>"succeeded" or "failed".</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>Subset of the catalog's /info response that the order service needs.</summary>
public class BookInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
