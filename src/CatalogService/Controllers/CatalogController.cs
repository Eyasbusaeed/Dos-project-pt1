using CatalogService.Data;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("/")]
public class CatalogController : ControllerBase
{
    private readonly CatalogRepository _repo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(CatalogRepository repo, IHttpClientFactory hf, IConfiguration config, ILogger<CatalogController> logger)
    {
        _repo = repo;
        _httpClientFactory = hf;
        _config = config;
        _logger = logger;
    }

    // GET /search/{topic}
    [HttpGet("search/{topic}")]
    public IActionResult Search(string topic)
    {
        var books = _repo.GetByTopic(topic);
        if (books.Count == 0) return NotFound("No items found for the given topic.");
        return Ok(books.Select(b => new { id = b.Id, title = b.Title }));
    }

    // GET /info/{id}
    [HttpGet("info/{id:int}")]
    public IActionResult Info(int id)
    {
        var book = _repo.GetById(id);
        if (book == null) return NotFound("Item not found.");
        return Ok(book);
    }

    // POST /update/{id}
    public class UpdateRequest
    {
        public int? Quantity { get; set; }
        public decimal? Price { get; set; }
        public bool Internal { get; set; } = false;
    }

    [HttpPost("update/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRequest req)
    {
        var books = _repo.GetAll();
        var book = books.FirstOrDefault(b => b.Id == id);
        if (book == null) return NotFound("Item not found.");

        if (req.Quantity.HasValue) book.Quantity = req.Quantity.Value;
        if (req.Price.HasValue) book.Price = req.Price.Value;
        _repo.SaveAll(books);

        if (!req.Internal)
        {
            var replicaName = _config["REPLICA"] == "catalog1" ? "catalog2" : "catalog1";
            var replicaUrl = $"http://{replicaName}:4000/update/{id}";
            try
            {
                var client = _httpClientFactory.CreateClient();
                await client.PostAsJsonAsync(replicaUrl, new UpdateRequest
                {
                    Quantity = req.Quantity,
                    Price = req.Price,
                    Internal = true
                });
                _logger.LogInformation("Replicated update to {Replica}", replicaName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Replica sync failed: {Msg}", ex.Message);
            }
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            await client.PostAsync($"http://frontend:5000/invalidate/{id}", null);
            _logger.LogInformation("Cache for book {Id} invalidated on frontend.", id);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to invalidate cache: {Msg}", ex.Message);
        }

        return Ok("Item updated successfully.");
    }
}