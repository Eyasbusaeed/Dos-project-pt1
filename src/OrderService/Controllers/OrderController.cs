using Microsoft.AspNetCore.Mvc;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("/")]
public class OrderController : ControllerBase
{
    private readonly OrderRepository _orderRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrderController> _logger;

    private readonly string[] _catalogReplicas = { "http://catalog1:4000", "http://catalog2:4000" };
    private static int _catalogIndex = 0;
    private static readonly object _indexLock = new();

    public OrderController(OrderRepository repo, IHttpClientFactory hf, ILogger<OrderController> logger)
    {
        _orderRepo = repo;
        _httpClientFactory = hf;
        _logger = logger;
    }

    private string GetCatalogReplica()
    {
        lock (_indexLock)
        {
            var url = _catalogReplicas[_catalogIndex];
            _catalogIndex = (_catalogIndex + 1) % _catalogReplicas.Length;
            return url;
        }
    }

    [HttpPost("purchase/{id:int}")]
    public async Task<IActionResult> Purchase(int id)
    {
        var client = _httpClientFactory.CreateClient();
        try
        {
            var catalogUrl = GetCatalogReplica();
            var response = await client.GetAsync($"{catalogUrl}/info/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound(new { message = "Book not found" });

            var book = await response.Content.ReadFromJsonAsync<BookInfo>();
            if (book == null) return NotFound(new { message = "Book not found" });

            _logger.LogInformation("Book fetched: {Title}, Qty: {Qty}", book.Title, book.Quantity);

            Order order;
            IActionResult result;

            if (book.Quantity > 0)
            {
                var newQty = book.Quantity - 1;
                var updatePayload = new { quantity = newQty, price = book.Price, @internal = false };
                await client.PostAsJsonAsync($"http://catalog1:4000/update/{id}", updatePayload);

                order = new Order
                {
                    Title = book.Title,
                    NewQuantity = newQty,
                    Price = book.Price,
                    State = "succeeded"
                };
                result = Ok(new { message = "Book purchased successfully" });
            }
            else
            {
                order = new Order
                {
                    Title = book.Title,
                    NewQuantity = book.Quantity,
                    Price = book.Price,
                    State = "failed"
                };
                result = BadRequest(new { message = "Book out of stock" });
            }

            _orderRepo.AddOrder(order);
            _logger.LogInformation("Order saved: {Order}", System.Text.Json.JsonSerializer.Serialize(order));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Purchase error: {Msg}", ex.Message);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}