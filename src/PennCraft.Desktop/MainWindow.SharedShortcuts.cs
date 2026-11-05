using System.Windows;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    internal async Task MoveSharedShortcutsAsync(string[] paths, bool confirmed = false)
    {
        if (IsPortal || !CanBeginTransfer(false) || Transfers is null) return;
        paths = paths.Where(path => SharedDesktopShortcut.Is(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0) return;
        if (!confirmed)
        {
            var message = $"Move {paths.Length} shared desktop app shortcut(s) into this Pen?{Environment.NewLine}{Environment.NewLine}"
                + "This removes the shared shortcut for all Windows users. Windows may request administrator permission."
                + $"{Environment.NewLine}{Environment.NewLine}Yes moves the shortcuts. No keeps references and leaves the shared desktop unchanged.";
            if (MessageBox.Show(message, "Move shared shortcuts", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                settings = settings with { ItemPaths = PenMembership.Normalize(settings.ItemPaths.Concat(paths)) };
                RefreshItemViews();
                await PersistItemChangesAsync([]);
                if (!moveSettingsBlocked) ItemStatus.Text = "Shared desktop references added; the original shortcuts were left in place.";
                return;
            }
        }
        foreach (var path in paths)
            if (!await Transfers.MoveSharedShortcutIntoPenAsync(this, path)) return;
    }
}
