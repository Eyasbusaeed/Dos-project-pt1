using OrderService.Models;

namespace OrderService.Data;

public class OrderRepository
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public OrderRepository(IConfiguration config)
    {
        _filePath = config["OrdersFilePath"] ?? "/app/data/orders.csv";
        EnsureFileExists();
    }

    private void EnsureFileExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "Title,NewQuantity,Price,State\n");
        }
    }

    public void AddOrder(Order order)
    {
        lock (_lock)
        {
            var line = $"\"{order.Title}\",{order.NewQuantity},{order.Price},{order.State}\n";
            File.AppendAllText(_filePath, line);
        }
    }
}