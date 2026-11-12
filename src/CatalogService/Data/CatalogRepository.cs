using System.Globalization;
using CatalogService.Models;

namespace CatalogService.Data;

/// <summary>
/// CSV-backed catalog storage. Every access (read or write) happens under a single lock,
/// and every modification is a read-modify-write performed entirely inside that lock, so
/// concurrent requests can never lose an update or oversell a book.
/// </summary>
public class CatalogRepository
{
    private const string Header = "Id,Title,Quantity,Price,Topic";

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly ILogger<CatalogRepository> _logger;

    public CatalogRepository(IConfiguration config, ILogger<CatalogRepository> logger)
    {
        _logger = logger;
        // Data path comes from config / environment (CatalogFilePath), default /app/data/catalog.csv.
        _filePath = config["CatalogFilePath"] ?? "/app/data/catalog.csv";
        EnsureFileExists();
    }

    /// <summary>Creates the data file with the four initial books if it does not exist yet.</summary>
    private void EnsureFileExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(_filePath))
        {
            _logger.LogInformation("Using existing catalog data file {Path}", _filePath);
            return;
        }

        var initial = new List<Book>
        {
            new() { Id = 1, Title = "How to get a good grade in DOS in 40 minutes a day", Quantity = 10, Price = 30, Topic = "distributed systems" },
            new() { Id = 2, Title = "RPCs for Noobs", Quantity = 10, Price = 50, Topic = "distributed systems" },
            new() { Id = 3, Title = "Xen and the Art of Surviving Undergraduate School", Quantity = 10, Price = 15, Topic = "undergraduate school" },
            new() { Id = 4, Title = "Cooking for the Impatient Undergrad", Quantity = 10, Price = 15, Topic = "undergraduate school" },
        };
        lock (_lock) Write(initial);
        _logger.LogInformation("Created new catalog data file {Path} with {Count} books", _filePath, initial.Count);
    }

    /// <summary>Returns a snapshot of all books.</summary>
    public List<Book> GetAll()
    {
        lock (_lock) return Read();
    }

    public Book? GetById(int id) => GetAll().FirstOrDefault(b => b.Id == id);

    public List<Book> GetByTopic(string topic) =>
        GetAll().Where(b => b.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Atomic read-modify-write on one book. <paramref name="mutate"/> runs while the lock is
    /// held; if it returns true the change is persisted, if false nothing is written.
    /// Returns (found, changed, bookAfter). The returned book is a copy.
    /// </summary>
    public (bool Found, bool Changed, Book? Book) Update(int id, Func<Book, bool> mutate)
    {
        lock (_lock)
        {
            var books = Read();
            var book = books.FirstOrDefault(b => b.Id == id);
            if (book == null) return (false, false, null);

            var changed = mutate(book);
            if (changed) Write(books);
            return (true, changed, book);
        }
    }

    // ---- file access (callers must hold _lock) ----

    private List<Book> Read()
    {
        var books = new List<Book>();
        foreach (var raw in File.ReadAllLines(_filePath).Skip(1))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var p = ParseCsvLine(line);
            if (p.Count < 5) continue;

            books.Add(new Book
            {
                Id = int.Parse(p[0], CultureInfo.InvariantCulture),
                Title = p[1],
                Quantity = int.Parse(p[2], CultureInfo.InvariantCulture),
                Price = decimal.Parse(p[3], CultureInfo.InvariantCulture),
                Topic = p[4],
            });
        }
        return books;
    }

    /// <summary>Writes to a temp file and then replaces the real file, so a crash mid-write cannot corrupt the data.</summary>
    private void Write(List<Book> books)
    {
        var lines = new List<string> { Header };
        foreach (var b in books)
        {
            lines.Add(string.Join(",",
                b.Id.ToString(CultureInfo.InvariantCulture),
                Quote(b.Title),
                b.Quantity.ToString(CultureInfo.InvariantCulture),
                b.Price.ToString(CultureInfo.InvariantCulture),
                Quote(b.Topic)));
        }

        var tmp = _filePath + ".tmp";
        File.WriteAllLines(tmp, lines);
        File.Move(tmp, _filePath, overwrite: true);
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    /// <summary>Minimal CSV line parser supporting quoted fields and "" escapes.</summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }
}
