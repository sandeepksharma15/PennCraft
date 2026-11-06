using System.Drawing;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PennCraft.Desktop;

internal sealed record PenTrayEntry(string Title, bool Visible, Action Show, Action Hide, Action Reset);

/// <summary>One notification icon for the workspace, including all hidden Pens.</summary>
internal sealed class TrayCommands : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();

    public TrayCommands(Dispatcher dispatcher, Func<IEnumerable<PenTrayEntry>> entries,
        Action showAll, Action hideAll, Action create, Func<bool> creationEnabled, Action toggleCreation, Action exit,
        Action? arrange = null, Action? undoLayout = null, Func<bool>? canUndoLayout = null,
        Action? saveLayout = null, Action? manageLayouts = null, Action? undoRestore = null, Func<bool>? canUndoRestore = null)
    {
        void Add(Forms.ToolStripItemCollection target, string title, Action action) =>
            target.Add(title, null, (_, _) => dispatcher.InvokeAsync(action));
        menu.Opening += (_, e) =>
        {
            foreach (Forms.ToolStripItem item in menu.Items.Cast<Forms.ToolStripItem>().ToArray()) item.Dispose();
            menu.Items.Clear();
            Add(menu.Items, "New Pen", create);
            if (arrange is not null) Add(menu.Items, "Arrange Pens automatically", arrange);
            if (undoLayout is not null)
            {
                var undo = menu.Items.Add("Undo last automatic layout", null, (_, _) => dispatcher.InvokeAsync(undoLayout));
                undo.Enabled = canUndoLayout?.Invoke() == true;
            }
            if (saveLayout is not null) Add(menu.Items, "Save current layout…", saveLayout);
            if (manageLayouts is not null) Add(menu.Items, "Saved layouts…", manageLayouts);
            if (undoRestore is not null)
            {
                var undo = menu.Items.Add("Undo last layout restore", null, (_, _) => dispatcher.InvokeAsync(undoRestore));
                undo.Enabled = canUndoRestore?.Invoke() == true;
            }
            foreach (var entry in entries())
            {
                var pen = new Forms.ToolStripMenuItem(entry.Title) { Checked = entry.Visible };
                Add(pen.DropDownItems, "Show", entry.Show);
                Add(pen.DropDownItems, "Hide", entry.Hide);
                Add(pen.DropDownItems, "Reset position and show", entry.Reset);
                menu.Items.Add(pen);
            }
            menu.Items.Add(new Forms.ToolStripSeparator());
            Add(menu.Items, "Show all Pens", showAll);
            Add(menu.Items, "Hide all Pens", hideAll);
            var gesture = new Forms.ToolStripMenuItem("Desktop rectangle creation") { Checked = creationEnabled() };
            gesture.Click += (_, _) => dispatcher.InvokeAsync(toggleCreation);
            menu.Items.Add(gesture);
            Add(menu.Items, "Exit PennCraft", exit);
            e.Cancel = false;
        };
        icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application, Text = "PennCraft", ContextMenuStrip = menu, Visible = true
        };
        icon.DoubleClick += (_, _) => dispatcher.InvokeAsync(showAll);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
    }
}
