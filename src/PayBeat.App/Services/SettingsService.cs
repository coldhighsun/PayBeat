using PayBeat.App.Models;

namespace PayBeat.App.Services;

/// <summary>
/// Loads and saves <see cref="SalarySettings"/> as JSON at <c>%APPDATA%\PayBeat\settings.json</c>.
/// Returns default settings when the file is absent or unreadable.
/// </summary>
public class SettingsService
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "PayBeat", "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new TimeOnlyConverter() }
    };

    /// <summary>
    /// Reads settings from disk. Returns a default <see cref="SalarySettings"/> instance
    /// if the file does not exist or cannot be deserialized.
    /// </summary>
    public SalarySettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SalarySettings();
        }
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<SalarySettings>(json, Options) ?? new SalarySettings();
        }
        catch
        {
            return new SalarySettings();
        }
    }

    /// <summary>
    /// Serializes <paramref name="settings"/> to disk, creating the directory if needed.
    /// </summary>
    /// <param name="settings">Settings to persist.</param>
    public void Save(SalarySettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }

    /// <summary>
    /// Serializes <see cref="TimeOnly"/> as an <c>HH:mm</c> string so the JSON file is human-readable.
    /// </summary>
    private sealed class TimeOnlyConverter : JsonConverter<TimeOnly>
    {
        /// <inheritdoc/>
        public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => TimeOnly.ParseExact(reader.GetString()!, "HH:mm");

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("HH:mm"));
    }
}