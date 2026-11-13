using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace FrontendService.Controllers;

/// <summary>
/// Thin gateway: search and info go to the catalog service, purchase goes to the order service.
/// Status codes and JSON bodies from the backends are passed through unchanged.
/// </summary>
[ApiController]
[Route("/")]
public class FrontendController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FrontendController> _logger;
    private readonly string _catalogUrl;
    private readonly string _orderUrl;

    public FrontendController(IHttpClientFactory hf, IConfiguration config, ILogger<FrontendController> logger)
    {
        _httpClientFactory = hf;
        _logger = logger;
        // Backend locations come from environment variables (set in docker-compose.yml).
        _catalogUrl = (config["CATALOG_URL"] ?? "http://catalog:4000").TrimEnd('/');
        _orderUrl = (config["ORDER_URL"] ?? "http://order:3000").TrimEnd('/');
    }

    /// <summary>GET /search/{topic} -> catalog GET /search/{topic}</summary>
    [HttpGet("search/{topic}")]
    public Task<IActionResult> Search(string topic)
    {
        _logger.LogInformation("GET /search/{Topic} -> catalog", topic);
        return Forward(HttpMethod.Get, $"{_catalogUrl}/search/{Uri.EscapeDataString(topic)}", "catalog");
    }

    /// <summary>GET /info/{id} -> catalog GET /info/{id}</summary>
    [HttpGet("info/{id:int}")]
    public Task<IActionResult> Info(int id)
    {
        _logger.LogInformation("GET /info/{Id} -> catalog", id);
        return Forward(HttpMethod.Get, $"{_catalogUrl}/info/{id}", "catalog");
    }

    /// <summary>POST /purchase/{id} -> order POST /purchase/{id}</summary>
    [HttpPost("purchase/{id:int}")]
    public Task<IActionResult> Purchase(int id)
    {
        _logger.LogInformation("POST /purchase/{Id} -> order", id);
        return Forward(HttpMethod.Post, $"{_orderUrl}/purchase/{id}", "order");
    }

    /// <summary>Sends the request to a backend and relays its status code and JSON body.</summary>
    private async Task<IActionResult> Forward(HttpMethod method, string url, string backend)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(new HttpRequestMessage(method, url));
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("{Backend} answered {Status}", backend, (int)response.StatusCode);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = "application/json",
                Content = body,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("{Backend} service unreachable: {Msg}", backend, ex.Message);
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new { message = $"{backend} service unavailable" });
        }
    }
}
