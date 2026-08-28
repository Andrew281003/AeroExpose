using Microsoft.Win32;

namespace AeroExpose.Services;

internal sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AeroExpose";

    public void RegisterCurrentExecutable()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Windows could not determine the AeroExpose executable path.");
        }

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows could not open the current user's startup registry key.");

        runKey.SetValue(ValueName, $"{Quote(executablePath)} --background", RegistryValueKind.String);
    }

    public void Unregister()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public bool IsRegistered()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return runKey?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    private static string Quote(string path) => $"\"{path}\"";
}
