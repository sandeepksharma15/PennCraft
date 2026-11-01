using System.Drawing;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PennCraft.Desktop;

/// <summary>Owns the notification icon and routes callbacks to the WPF dispatcher.</summary>
internal sealed class TrayCommands : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();

    public TrayCommands(Dispatcher dispatcher, Action show, Action hide, Action reset, Action exit)
    {
        void Add(string title, Action action) =>
            menu.Items.Add(title, null, (_, _) => dispatcher.InvokeAsync(action));
        Add("Show Pen", show);
        Add("Hide Pen", hide);
        Add("Reset position and show", reset);
        menu.Items.Add(new Forms.ToolStripSeparator());
        Add("Exit PennCraft", exit);
        icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PennCraft",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => dispatcher.InvokeAsync(show);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
    }
}
