using PayBeat.App.Models;

namespace PayBeat.App.Services;

/// <summary>
/// Loads and saves <see cref="SalarySettings"/> as JSON at <c>%APPDATA%\PayBeat\settings.json</c>.
/// Returns default settings when the file is absent or unreadable. Caches the last-read/written
/// instance in memory so repeated <see cref="Load"/> calls (display-mode switches, hotkey changes,
/// settings saves) don't re-read and re-deserialize the file each time; a single instance of this
/// service is expected to live for the app's whole lifetime.
/// </summary>
public class SettingsService
{
    /// <summary>
    /// Default location of the settings file under the per-user application data folder.
    /// </summary>
    private static readonly string DefaultFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "PayBeat", "settings.json");

    /// <summary>
    /// Full path of the settings file this instance reads from and writes to.
    /// </summary>
    private readonly string _filePath;

    /// <summary>
    /// Creates a settings service bound to <paramref name="filePath"/>, or to the per-user
    /// default location when it is <see langword="null"/>.
    /// </summary>
    /// <param name="filePath">Optional override of the settings file path (used by tests).</param>
    public SettingsService(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath;
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new TimeOnlyConverter(), new DisplayModeConverter() }
    };

    /// <summary>
    /// In-memory copy of the last settings read from or written to disk. <see cref="SalarySettings"/>
    /// is an immutable record, so it's safe to hand the same cached instance out to every caller.
    /// </summary>
    private SalarySettings? _cache;

    /// <summary>
    /// Returns the current settings, reading them from disk only once per process (subsequent
    /// calls return the cached instance kept in sync by <see cref="Save"/>). Returns a default
    /// <see cref="SalarySettings"/> instance if the file does not exist or cannot be deserialized.
    /// </summary>
    public SalarySettings Load()
    {
        if (_cache is { } cached)
        {
            return cached;
        }

        if (!File.Exists(_filePath))
        {
            return _cache = new SalarySettings();
        }
        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<SalarySettings>(json, Options) ?? new SalarySettings();
            return _cache = Normalize(settings);
        }
        catch
        {
            BackupCorruptFile();
            // Don't cache this fallback: a transient read failure (e.g. a brief antivirus lock)
            // should be retried on the next Load() instead of permanently sticking as defaults.
            return new SalarySettings();
        }
    }

    /// <summary>
    /// Repairs semantically invalid values that can slip past JSON deserialization (e.g. a
    /// hand-edited file), such as a zero-length work schedule where <see cref="SalarySettings.WorkStart"/>
    /// equals <see cref="SalarySettings.WorkEnd"/> — which would otherwise make <c>EarningsCalculator</c>
    /// treat every moment of the day as post-workday and pay out the full daily salary immediately.
    /// A schedule where <c>WorkEnd</c> is earlier than <c>WorkStart</c> is a valid overnight shift
    /// and is left as-is.
    /// </summary>
    private static SalarySettings Normalize(SalarySettings settings)
    {
        if (settings.WorkStart == settings.WorkEnd)
        {
            var defaults = new SalarySettings();
            settings = settings with { WorkStart = defaults.WorkStart, WorkEnd = defaults.WorkEnd };
        }

        return settings;
    }

    /// <summary>
    /// Serializes <paramref name="settings"/> to disk, creating the directory if needed.
    /// </summary>
    /// <param name="settings">Settings to persist.</param>
    /// <returns><see langword="true"/> if the write succeeded; <see langword="false"/> if a
    /// disk or permission error prevented it (the app doesn't crash, but callers should surface
    /// the failure instead of assuming the settings were persisted).</returns>
    public bool Save(SalarySettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, Options));
            _cache = settings;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort; a locked/full disk or permission issue shouldn't crash the app.
            // Leave the cache as-is: the write failed, so disk still holds the last-saved value.
            return false;
        }
    }

    /// <summary>
    /// Preserves an unreadable settings file as <c>settings.json.bak</c> (overwriting any prior
    /// backup) so a load failure doesn't silently destroy the user's saved position/preferences.
    /// Swallows failures — this is a best-effort safety net, not a critical path.
    /// </summary>
    private void BackupCorruptFile()
    {
        try
        {
            File.Copy(_filePath, _filePath + ".bak", overwrite: true);
        }
        catch
        {
            // Best-effort; if we can't back it up, still fall back to defaults.
        }
    }

    /// <summary>
    /// Serializes <see cref="DisplayMode"/> as its name (e.g. <c>"Flex"</c>) instead of an ordinal
    /// number, so persisted values remain stable across future enum reordering. Any value that
    /// can't be parsed as a known name — including old files that stored the ordinal number —
    /// falls back to <see cref="DisplayMode.None"/> instead of failing the whole settings load.
    /// </summary>
    private sealed class DisplayModeConverter : JsonConverter<DisplayMode>
    {
        /// <inheritdoc/>
        public override DisplayMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String &&
                Enum.TryParse<DisplayMode>(reader.GetString(), ignoreCase: true, out var mode))
            {
                return mode;
            }

            return DisplayMode.None;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, DisplayMode value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
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