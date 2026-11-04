using System.Text.Json;
using Kontext.Contracts;

namespace Kontext.Sessions;

internal sealed class JsonHiddenRepositoryStore(SessionsOptions options) : IHiddenRepositoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Lock _gate = new();
    private readonly string _filePath = Path.Combine(options.StateDirectory, "hidden-repositories.json");

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

    public void Save(IReadOnlySet<string> hidden)
    {
        ArgumentNullException.ThrowIfNull(hidden);

        lock (_gate)
        {
            Directory.CreateDirectory(options.StateDirectory);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(hidden.Order(StringComparer.OrdinalIgnoreCase), JsonOptions));
        }
    }
}
