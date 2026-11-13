using OrderService.Data;

// Order service: handles purchases. Talks to the catalog service over HTTP and keeps a
// persistent order log. Listens on port 3000.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<OrderRepository>();
builder.Services.AddHttpClient();

builder.WebHost.UseUrls("http://0.0.0.0:3000");

var app = builder.Build();

app.MapControllers();
app.Run();
