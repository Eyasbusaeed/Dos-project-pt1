using CatalogService.Data;

// Catalog service: owns the book data (id, title, stock, price, topic).
// Listens on port 4000 inside the container / on the host.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// One repository instance for the whole process; it serialises file access with a lock.
builder.Services.AddSingleton<CatalogRepository>();

builder.WebHost.UseUrls("http://0.0.0.0:4000");

var app = builder.Build();

app.MapControllers();
app.Run();
