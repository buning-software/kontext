using System.Text.Json;

namespace Kontext.Sessions;

/// <summary>A small case-insensitive set of names kept in one JSON file. A missing or corrupt file reads as empty.</summary>
internal sealed class JsonNameSetFile(string directory, string fileName)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Lock _gate = new();
    private readonly string _filePath = Path.Combine(directory, fileName);

    public IReadOnlySet<string> Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var names = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_filePath)) ?? [];
                return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public void Save(IReadOnlySet<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        lock (_gate)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(names.Order(StringComparer.OrdinalIgnoreCase), JsonOptions));
        }
    }
}
