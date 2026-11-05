using System.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace PennCraft.Desktop.Items;

public sealed class PenItem(string path) : INotifyPropertyChanged
{
    private ImageSource? icon;
    private bool isSelected;

    public string Path { get; } = path;
    public string DisplayName
    {
        get
        {
            if (VirtualDesktopItem.IsRecycleBin(Path)) return "Recycle Bin";
            var name = System.IO.Path.GetFileName(Path);
            if (string.IsNullOrEmpty(name)) return Path;
            var display = System.IO.Path.GetExtension(name).ToLowerInvariant() is ".lnk" or ".url"
                ? System.IO.Path.GetFileNameWithoutExtension(name) : name;
            return SharedDesktopShortcut.Is(Path) ? $"{display} (reference)" : display;
        }
    }
    public string ToolTip => VirtualDesktopItem.IsRecycleBin(Path) ? "Recycle Bin — Windows system item"
        : PenMembership.Exists(Path)
        ? Path : $"Unavailable: {Path}\nThe reference is retained. Restore the item or remove it from the Pen.";

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public ImageSource? Icon
    {
        get => icon;
        set
        {
            icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
