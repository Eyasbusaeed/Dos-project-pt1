using CatalogService.Models;

namespace CatalogService.Data;

public class CatalogRepository
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public CatalogRepository(IConfiguration config)
    {
        _filePath = config["CatalogFilePath"] ?? "/app/data/catalog.csv";
        EnsureFileExists();
    }

    private void EnsureFileExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_filePath))
        {
            var lines = new List<string>
            {
                "Id,Title,Quantity,Price,Topic",
                "1,\"How to get a good grade in DOS in 40 minutes a day\",10,30,\"distributed systems\"",
                "2,\"RPCs for Noobs\",10,50,\"distributed systems\"",
                "3,\"Xen and the Art of Surviving Undergraduate School\",10,15,\"undergraduate school\"",
                "4,\"Cooking for the Impatient Undergrad\",10,15,\"undergraduate school\""
            };
            File.WriteAllLines(_filePath, lines);
        }
    }

    public List<Book> GetAll()
    {
        lock (_lock)
        {
            var books = new List<Book>();
            var lines = File.ReadAllLines(_filePath);
            
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = ParseCsvLine(line);
                if (parts.Count < 5) continue;

                books.Add(new Book
                {
                    Id = int.Parse(parts[0]),
                    Title = parts[1].Trim('"'),
                    Quantity = int.Parse(parts[2]),
                    Price = decimal.Parse(parts[3]),
                    Topic = parts[4].Trim('"')
                });
            }
            return books;
        }
    }

    public Book? GetById(int id) => GetAll().FirstOrDefault(b => b.Id == id);

    public List<Book> GetByTopic(string topic) =>
        GetAll().Where(b => b.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();

    public void SaveAll(List<Book> books)
    {
        lock (_lock)
        {
            var lines = new List<string> { "Id,Title,Quantity,Price,Topic" };
            foreach (var b in books)
            {
                lines.Add($"{b.Id},\"{b.Title}\",{b.Quantity},{b.Price},\"{b.Topic}\"");
            }
            File.WriteAllLines(_filePath, lines);
        }
    }

    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        string current = "";

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                current += c;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current);
        return result;
    }
}