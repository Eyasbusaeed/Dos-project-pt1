using System.Net;
using Microsoft.AspNetCore.Mvc;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Controllers;

/// <summary>REST API of the order service. All responses are JSON.</summary>
[ApiController]
[Route("/")]
public class OrderController : ControllerBase
{
    private readonly OrderRepository _orders;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrderController> _logger;
    private readonly string _catalogUrl;

    public OrderController(OrderRepository orders, IHttpClientFactory hf, IConfiguration config, ILogger<OrderController> logger)
    {
        _orders = orders;
        _httpClientFactory = hf;
        _logger = logger;
        // Catalog location comes from the CATALOG_URL environment variable (docker compose sets it).
        _catalogUrl = (config["CATALOG_URL"] ?? "http://catalog:4000").TrimEnd('/');
    }

    /// <summary>
    /// POST /purchase/{id}. Looks the book up in the catalog (404 if unknown), then asks the catalog
    /// to atomically decrement its stock. If the catalog answers 409 the purchase fails (out of stock).
    /// Every attempt that reaches a known book is logged in the order CSV.
    /// </summary>
    [HttpPost("purchase/{id:int}")]
    public async Task<IActionResult> Purchase(int id)
    {
        _logger.LogInformation("POST /purchase/{Id}", id);
        var client = _httpClientFactory.CreateClient();
        try
        {
            // Step 1: verify the item exists.
            var infoResp = await client.GetAsync($"{_catalogUrl}/info/{id}");
            if (infoResp.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Purchase of unknown book {Id}", id);
                return NotFound(new { message = $"Book {id} not found" });
            }
            infoResp.EnsureSuccessStatusCode();
            var book = await infoResp.Content.ReadFromJsonAsync<BookInfo>();
            if (book == null) throw new InvalidOperationException("Empty response from catalog");

            // Step 2: atomic check-and-decrement inside the catalog (no read-then-write race here).
            var decResp = await client.PostAsync($"{_catalogUrl}/decrement/{id}", null);

            if (decResp.StatusCode == HttpStatusCode.Conflict)
            {
                var failed = _orders.Add(book.Id, book.Title, book.Price, "failed");
                _logger.LogWarning("Order {OrderId} failed: '{Title}' is out of stock", failed.OrderId, book.Title);
                return Conflict(new { message = "Book out of stock", orderId = failed.OrderId, title = book.Title });
            }
            if (decResp.StatusCode == HttpStatusCode.NotFound)
                return NotFound(new { message = $"Book {id} not found" });
            decResp.EnsureSuccessStatusCode();

            var order = _orders.Add(book.Id, book.Title, book.Price, "succeeded");
            _logger.LogInformation("bought book {Title}", book.Title);
            return Ok(new { message = "Book purchased successfully", orderId = order.OrderId, title = book.Title });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Catalog service unreachable or failed: {Msg}", ex.Message);
            return StatusCode(503, new { message = "Catalog service unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Purchase error");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>GET /orders - returns the whole persistent order log.</summary>
    [HttpGet("orders")]
    public IActionResult List()
    {
        var all = _orders.GetAll();
        _logger.LogInformation("GET /orders -> {Count} order(s)", all.Count);
        return Ok(all.Select(o => new
        {
            orderId = o.OrderId,
            timestampUtc = o.TimestampUtc,
            bookId = o.BookId,
            title = o.Title,
            price = o.Price,
            status = o.Status,
        }));
    }
}
