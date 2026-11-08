namespace OrderService.Models;

public class Order
{
    public string Title { get; set; } = string.Empty;
    public int NewQuantity { get; set; }
    public decimal Price { get; set; }
    public string State { get; set; } = string.Empty;
}

public class BookInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}