using CatalogService.Data;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

/// <summary>Body of the update request. All fields optional; at least one must be present.</summary>
public class UpdateRequest
{
    /// <summary>New absolute price.</summary>
    public decimal? Price { get; set; }
    /// <summary>New absolute stock quantity.</summary>
    public int? Quantity { get; set; }
    /// <summary>Relative stock change (+ increases, - decreases).</summary>
    public int? Delta { get; set; }
}

/// <summary>REST API of the catalog service. All responses are JSON.</summary>
[ApiController]
[Route("/")]
public class CatalogController : ControllerBase
{
    private readonly CatalogRepository _repo;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(CatalogRepository repo, ILogger<CatalogController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>GET /search/{topic} - query by subject. Returns [{id,title}], empty list if none.</summary>
    [HttpGet("search/{topic}")]
    public IActionResult Search(string topic)
    {
        var books = _repo.GetByTopic(topic);
        _logger.LogInformation("GET /search/{Topic} -> {Count} result(s)", topic, books.Count);
        return Ok(books.Select(b => new { id = b.Id, title = b.Title }));
    }

    /// <summary>GET /info/{id} - query by item. 404 if the book does not exist.</summary>
    [HttpGet("info/{id:int}")]
    public IActionResult Info(int id)
    {
        var book = _repo.GetById(id);
        if (book == null)
        {
            _logger.LogWarning("GET /info/{Id} -> 404 not found", id);
            return NotFound(new { message = $"Book {id} not found" });
        }
        _logger.LogInformation("GET /info/{Id} -> {Title} (qty {Qty}, price {Price})", id, book.Title, book.Quantity, book.Price);
        return Ok(new { id = book.Id, title = book.Title, quantity = book.Quantity, price = book.Price, topic = book.Topic });
    }

    /// <summary>
    /// PUT or POST /update/{id} - change price and/or stock. "quantity" sets an absolute value,
    /// "delta" adds/subtracts. The whole change is atomic and never allows negative stock.
    /// </summary>
    [HttpPut("update/{id:int}")]
    [HttpPost("update/{id:int}")]
    public IActionResult Update(int id, [FromBody] UpdateRequest req)
    {
        if (req.Price == null && req.Quantity == null && req.Delta == null)
            return BadRequest(new { message = "Provide at least one of: price, quantity, delta" });
        if (req.Price is < 0)
            return BadRequest(new { message = "price must not be negative" });
        if (req.Quantity is < 0)
            return BadRequest(new { message = "quantity must not be negative" });

        string? error = null;
        var (found, changed, book) = _repo.Update(id, b =>
        {
            // Compute the resulting stock first so nothing is modified if it would go negative.
            int newQty = req.Quantity ?? b.Quantity;
            if (req.Delta.HasValue) newQty += req.Delta.Value;
            if (newQty < 0)
            {
                error = $"Update would make stock negative (would be {newQty})";
                return false;
            }
            b.Quantity = newQty;
            if (req.Price.HasValue) b.Price = req.Price.Value;
            return true;
        });

        if (!found)
        {
            _logger.LogWarning("UPDATE /update/{Id} -> 404 not found", id);
            return NotFound(new { message = $"Book {id} not found" });
        }
        if (!changed)
        {
            _logger.LogWarning("UPDATE /update/{Id} rejected: {Error}", id, error);
            return BadRequest(new { message = error });
        }

        _logger.LogInformation("UPDATE /update/{Id} (price={Price}, quantity={Qty}, delta={Delta}) -> qty {NewQty}, price {NewPrice}",
            id, req.Price, req.Quantity, req.Delta, book!.Quantity, book.Price);
        return Ok(new { id = book.Id, title = book.Title, quantity = book.Quantity, price = book.Price, topic = book.Topic });
    }

    /// <summary>
    /// POST /decrement/{id} - atomically reduce stock by one. Check and decrement happen under the
    /// repository lock, so concurrent purchases can never oversell. 409 when out of stock.
    /// </summary>
    [HttpPost("decrement/{id:int}")]
    public IActionResult Decrement(int id)
    {
        var (found, changed, book) = _repo.Update(id, b =>
        {
            if (b.Quantity <= 0) return false;
            b.Quantity -= 1;
            return true;
        });

        if (!found)
        {
            _logger.LogWarning("POST /decrement/{Id} -> 404 not found", id);
            return NotFound(new { message = $"Book {id} not found" });
        }
        if (!changed)
        {
            _logger.LogWarning("POST /decrement/{Id} -> 409 out of stock ({Title})", id, book!.Title);
            return Conflict(new { message = "Book out of stock", id = book.Id, title = book.Title, quantity = book.Quantity });
        }

        _logger.LogInformation("POST /decrement/{Id} -> {Title}, remaining stock {Qty}", id, book!.Title, book.Quantity);
        return Ok(new { id = book.Id, title = book.Title, quantity = book.Quantity, price = book.Price, topic = book.Topic });
    }
}
