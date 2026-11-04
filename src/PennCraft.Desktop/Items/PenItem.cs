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
            var name = System.IO.Path.GetFileName(Path);
            if (string.IsNullOrEmpty(name)) return Path;
            return System.IO.Path.GetExtension(name).ToLowerInvariant() is ".lnk" or ".url"
                ? System.IO.Path.GetFileNameWithoutExtension(name) : name;
        }
    }
    public string ToolTip => PenMembership.Exists(Path)
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
