using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using PennCraft.Desktop.Infrastructure;

namespace PennCraft.Desktop.Windows;

internal interface IDesktopIconSetting
{
    int? Read(string panel);
    void Write(string panel, int? value);
    void Refresh();
}

internal sealed class DesktopIconSetting : IDesktopIconSetting
{
    private const string Root = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\";
    private const string Name = "{645FF040-5081-101B-9F08-00AA002F954E}";

    public int? Read(string panel)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Root + panel);
        var value = key?.GetValue(Name);
        return value switch { null => null, int number when number is 0 or 1 => number,
            _ => throw new InvalidDataException("Unexpected Recycle Bin visibility setting.") };
    }

    public void Write(string panel, int? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Root + panel);
        if (value.HasValue) key.SetValue(Name, value.Value, RegistryValueKind.DWord);
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }

    public void Refresh() => SHChangeNotify(0x08000000, 0, 0, 0);
    [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint change, uint flags, nint first, nint second);
}

internal sealed record RecycleBinBaseline([property: JsonRequired] int Version,
    [property: JsonRequired] int? NewStartPanel, [property: JsonRequired] int? ClassicStartMenu);

/// <summary>Owns only the two current-user Recycle Bin visibility values, with a durable restore record.</summary>
internal sealed class RecycleBinVisibility(string statePath, IDesktopIconSetting setting)
{
    public void Update(bool hide)
    {
        if (!hide) { Restore(); return; }
        if (!File.Exists(statePath))
            AtomicJsonFile.Write(statePath, new RecycleBinBaseline(1, setting.Read("NewStartPanel"), setting.Read("ClassicStartMenu")));
        else ReadBaseline(); // Never overwrite an unreadable recovery record.
        if (setting.Read("NewStartPanel") == 1 && setting.Read("ClassicStartMenu") == 1) return;
        setting.Write("NewStartPanel", 1);
        setting.Write("ClassicStartMenu", 1);
        setting.Refresh();
    }

    public void Restore()
    {
        if (!File.Exists(statePath)) return;
        var baseline = ReadBaseline();
        // Preserve an explicit external change made while PennCraft was running.
        if (setting.Read("NewStartPanel") == 1) setting.Write("NewStartPanel", baseline.NewStartPanel);
        if (setting.Read("ClassicStartMenu") == 1) setting.Write("ClassicStartMenu", baseline.ClassicStartMenu);
        setting.Refresh();
        File.Delete(statePath);
    }

    private RecycleBinBaseline ReadBaseline()
    {
        if (new FileInfo(statePath).LinkTarget is not null) throw new InvalidDataException("Linked visibility record.");
        var value = JsonSerializer.Deserialize<RecycleBinBaseline>(File.ReadAllText(statePath));
        if (value is not { Version: 1 } || value.NewStartPanel is not (null or 0 or 1) || value.ClassicStartMenu is not (null or 0 or 1))
            throw new InvalidDataException("Invalid visibility recovery record.");
        return value;
    }
}
