// Frontend service: the single entry point for clients. It exposes search/info/purchase and
// forwards them to the catalog and order services over HTTP. Listens on port 5000.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();

app.MapControllers();
app.Run();
