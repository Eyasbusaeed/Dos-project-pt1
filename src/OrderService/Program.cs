using OrderService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<OrderRepository>();
builder.Services.AddHttpClient();

builder.WebHost.UseUrls("http://0.0.0.0:3000");

var app = builder.Build();

app.MapControllers();
app.Run();