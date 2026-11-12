using System.Globalization;
using OrderService.Models;

namespace OrderService.Data;

/// <summary>
/// Append-only CSV order log. All access is under one lock so concurrent purchases get
/// unique, sequential order ids and never interleave partial lines.
/// </summary>
public class OrderRepository
{
    private const string Header = "OrderId,TimestampUtc,BookId,Title,Price,Status";

    private readonly string _filePath;
    private readonly object _lock = new();
    private int _lastId;

    public OrderRepository(IConfiguration config)
    {
        // Data path from config / environment (OrdersFilePath), default /app/data/orders.csv.
        _filePath = config["OrdersFilePath"] ?? "/app/data/orders.csv";

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_filePath))
            File.WriteAllText(_filePath, Header + "\n");

        // Continue numbering after the last persisted order (survives restarts).
        _lastId = ReadAll().Select(o => o.OrderId).DefaultIfEmpty(0).Max();
    }

    /// <summary>Assigns the next order id, appends the order to disk and returns it.</summary>
    public Order Add(int bookId, string title, decimal price, string status)
    {
        lock (_lock)
        {
            var order = new Order
            {
                OrderId = ++_lastId,
                TimestampUtc = DateTime.UtcNow,
                BookId = bookId,
                Title = title,
                Price = price,
                Status = status,
            };
            var line = string.Join(",",
                order.OrderId.ToString(CultureInfo.InvariantCulture),
                order.TimestampUtc.ToString("o", CultureInfo.InvariantCulture),
                order.BookId.ToString(CultureInfo.InvariantCulture),
                "\"" + order.Title.Replace("\"", "\"\"") + "\"",
                order.Price.ToString(CultureInfo.InvariantCulture),
                order.Status);
            File.AppendAllText(_filePath, line + "\n");
            return order;
        }
    }

    public List<Order> GetAll()
    {
        lock (_lock) return ReadAll();
    }

    private List<Order> ReadAll()
    {
        var orders = new List<Order>();
        foreach (var raw in File.ReadAllLines(_filePath).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var p = Split(raw.Trim());
            if (p.Count < 6) continue;
            orders.Add(new Order
            {
                OrderId = int.Parse(p[0], CultureInfo.InvariantCulture),
                TimestampUtc = DateTime.Parse(p[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                BookId = int.Parse(p[2], CultureInfo.InvariantCulture),
                Title = p[3],
                Price = decimal.Parse(p[4], CultureInfo.InvariantCulture),
                Status = p[5],
            });
        }
        return orders;
    }

    /// <summary>Minimal CSV splitter supporting quoted fields and "" escapes.</summary>
    private static List<string> Split(string line)
    {
        var result = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (q && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else q = !q;
            }
            else if (c == ',' && !q) { result.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        result.Add(cur.ToString());
        return result;
    }
}
