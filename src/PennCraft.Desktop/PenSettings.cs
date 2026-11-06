using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Windows;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Infrastructure;

namespace PennCraft.Desktop;

/// <summary>Durable user intent, independent of temporary native shell visibility.</summary>
public sealed record PenSettings
{
    public const double MinimumWidth = 240;
    public const double MinimumHeight = 120;
    internal const double FoldedHeight = 36;
    public string Title { get; init; } = "Programs";
    public double Left { get; init; } = 100;
    public double Top { get; init; } = 100;
    public double Width { get; init; } = 600;
    public double Height { get; init; } = 300;
    public double BackgroundOpacity { get; init; } = 0.78;
    public bool IsVisible { get; init; } = true;
    public bool IsFolded { get; init; }
    public string? PortalFolder { get; init; }
    public string[] ItemPaths { get; init; } = [];
    public TrackedItem[] TrackedItems { get; init; } = [];

    public PenSettings Normalize() => this with
    {
        ItemPaths = PenMembership.Normalize(ItemPaths),
        TrackedItems = ItemTracking.Normalize(ItemPaths ?? [], TrackedItems),
        Title = string.IsNullOrWhiteSpace(Title) ? "Programs" : Title.Trim()[..Math.Min(Title.Trim().Length, 80)],
        Left = double.IsFinite(Left) ? Left : 100,
        Top = double.IsFinite(Top) ? Top : 100,
        Width = double.IsFinite(Width) ? Math.Clamp(Width, MinimumWidth, 10000) : 600,
        Height = double.IsFinite(Height) ? Math.Clamp(Height, MinimumHeight, 10000) : 300,
        BackgroundOpacity = double.IsFinite(BackgroundOpacity) ? Math.Clamp(BackgroundOpacity, 0.2, 1) : 0.78
    };
}

/// <summary>Writes a complete replacement atomically; failed reads leave the original file intact.</summary>
public sealed class PenSettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public PenSettings Load()
    {
        try
        {
            if (!File.Exists(filePath)) return new();
            return (JsonSerializer.Deserialize<PenSettings>(File.ReadAllText(filePath)) ?? new()).Normalize();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Diagnostics.Write($"settings read failed: {exception.Message}");
            return new();
        }
    }

    public bool Save(PenSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            AtomicJsonFile.Write(filePath, settings.Normalize(), JsonOptions);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Diagnostics.Write($"settings write failed: {exception.Message}");
            return false;
        }
    }
}
