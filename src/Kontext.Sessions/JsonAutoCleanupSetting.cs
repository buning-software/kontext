using System.Text.Json;
using Kontext.Contracts;

namespace Kontext.Sessions;

/// <summary>Keeps <see cref="IAutoCleanupSetting"/> in a small JSON file. A missing or corrupt file reads as on.</summary>
internal sealed class JsonAutoCleanupSetting(SessionsOptions options) : IAutoCleanupSetting
{
    private const string FileName = "auto-cleanup.json";

    private readonly Lock _gate = new();
    private readonly string _filePath = Path.Combine(options.StateDirectory, FileName);

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                if (!File.Exists(_filePath))
                {
                    return true;
                }

                try
                {
                    return JsonSerializer.Deserialize<bool?>(File.ReadAllText(_filePath)) ?? true;
                }
                catch (JsonException)
                {
                    return true;
                }
            }
        }

        set
        {
            lock (_gate)
            {
                Directory.CreateDirectory(options.StateDirectory);
                File.WriteAllText(_filePath, JsonSerializer.Serialize(value));
            }
        }
    }
}
