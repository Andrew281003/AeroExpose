using System.IO;
using AeroExpose.Core.Services;
using AeroExpose.Core.Settings;

namespace AeroExpose.Services;

internal sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public SettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AeroExpose",
            "settings.json");
    }

    public async Task<MissionControlSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new MissionControlSettings();
            }

            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
            return SettingsSerializer.Deserialize(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return new MissionControlSettings();
        }
    }

    public async Task SaveAsync(MissionControlSettings settings, CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            settings.Normalize();
            var directory = Path.GetDirectoryName(_settingsPath)
                ?? throw new InvalidOperationException("The settings path has no directory.");
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                SettingsSerializer.Serialize(settings),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _settingsPath, true);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
