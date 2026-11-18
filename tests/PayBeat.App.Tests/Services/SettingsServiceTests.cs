using AwesomeAssertions;
using PayBeat.App.Models;
using PayBeat.App.Services;

namespace PayBeat.App.Tests.Services;

/// <summary>
/// Tests for <see cref="SettingsService"/> persistence and compatibility.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    /// <summary>
    /// Temporary directory holding the settings file for each test instance.
    /// </summary>
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"PayBeatTests-{Guid.NewGuid():N}");

    /// <summary>
    /// Gets the full path of the settings file under the temporary directory.
    /// </summary>
    private string FilePath => Path.Combine(_dir, "settings.json");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies defaults are returned when no file exists.
    /// </summary>
    [Fact]
    public void Load_FileMissing_ReturnsDefaults()
    {
        var service = new SettingsService(FilePath);

        service.Load().Should().Be(new SalarySettings());
    }

    /// <summary>
    /// Verifies a saved value round-trips through a fresh service.
    /// </summary>
    [Fact]
    public void Save_ThenLoadWithNewInstance_RoundTripsValues()
    {
        var settings = new SalarySettings
        {
            DailySalary = 1234m,
            DisplayMode = DisplayMode.Flex,
            WorkStart = new TimeOnly(8, 30),
            HotkeyVirtualKey = 0x41,
            AutoHideAtEdge = true,
        };

        new SettingsService(FilePath).Save(settings).Should().BeTrue();
        var loaded = new SettingsService(FilePath).Load();

        loaded.DailySalary.Should().Be(1234m);
        loaded.DisplayMode.Should().Be(DisplayMode.Flex);
        loaded.WorkStart.Should().Be(new TimeOnly(8, 30));
        loaded.HotkeyVirtualKey.Should().Be(0x41);
        loaded.AutoHideAtEdge.Should().BeTrue();
    }

    /// <summary>
    /// Verifies auto-hide is on by default.
    /// </summary>
    [Fact]
    public void Defaults_AutoHideAtEdge_IsEnabled()
    {
        new SalarySettings().AutoHideAtEdge.Should().BeTrue();
    }

    /// <summary>
    /// Verifies a corrupt file falls back to defaults and is backed up.
    /// </summary>
    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndCreatesBackup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        var loaded = new SettingsService(FilePath).Load();

        loaded.Should().Be(new SalarySettings());
        File.Exists(FilePath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// Verifies an equal start and end time is repaired to the default schedule.
    /// </summary>
    [Fact]
    public void Load_ZeroLengthSchedule_RestoresDefaultSchedule()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "WorkStart": "10:00", "WorkEnd": "10:00" }""");

        var loaded = new SettingsService(FilePath).Load();

        loaded.WorkStart.Should().Be(new SalarySettings().WorkStart);
        loaded.WorkEnd.Should().Be(new SalarySettings().WorkEnd);
    }

    /// <summary>
    /// Verifies legacy ordinal display modes fall back to <see cref="DisplayMode.None"/>.
    /// </summary>
    [Fact]
    public void Load_NumericDisplayMode_FallsBackToNone()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "DisplayMode": 1 }""");

        var loaded = new SettingsService(FilePath).Load();

        loaded.DisplayMode.Should().Be(DisplayMode.None);
    }

    /// <summary>
    /// Verifies the legacy Win32 hotkey fields are read unchanged.
    /// </summary>
    [Fact]
    public void Load_LegacyHotkeyFields_ArePreserved()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "HotkeyModifiers": 6, "HotkeyVirtualKey": 112 }""");

        var loaded = new SettingsService(FilePath).Load();

        loaded.HotkeyModifiers.Should().Be(6);
        loaded.HotkeyVirtualKey.Should().Be(112);
    }

    /// <summary>
    /// Verifies <see cref="SettingsService.Update"/> applies the change to the current settings and persists it.
    /// </summary>
    [Fact]
    public void Update_Change_IsAppliedAndPersisted()
    {
        var service = new SettingsService(FilePath);

        var saved = service.Update(settings => settings with { Currency = "$" });

        saved.Should().BeTrue();
        service.Load().Currency.Should().Be("$");
        new SettingsService(FilePath).Load().Currency.Should().Be("$");
    }

    /// <summary>
    /// Verifies concurrent updates from several threads do not overwrite each other.
    /// </summary>
    [Fact]
    public async Task Update_Concurrent_DoesNotLoseChanges()
    {
        var service = new SettingsService(FilePath);
        var start = service.Load().DailySalary;
        const int Count = 50;

        await Task.WhenAll(Enumerable.Range(0, Count).Select(_ =>
            Task.Run(() => service.Update(settings => settings with { DailySalary = settings.DailySalary + 1 }),
                TestContext.Current.CancellationToken)));

        service.Load().DailySalary.Should().Be(start + Count);
        new SettingsService(FilePath).Load().DailySalary.Should().Be(start + Count);
    }

    /// <summary>
    /// Verifies the last-check store keeps the other settings when it records a check time.
    /// </summary>
    [Fact]
    public async Task SetLastCheckedAtAsync_OtherSettings_AreKept()
    {
        var service = new SettingsService(FilePath);
        service.Save(service.Load() with { Currency = "€" });
        var store = new SettingsLastCheckStore(service);
        var checkedAt = DateTimeOffset.UtcNow;

        await store.SetLastCheckedAtAsync(checkedAt, TestContext.Current.CancellationToken);

        var loaded = new SettingsService(FilePath).Load();
        loaded.Currency.Should().Be("€");
        loaded.LastUpdateCheckUtc.Should().Be(checkedAt);
    }
}
