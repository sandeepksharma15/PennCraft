using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer shellTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly PenSettingsStore store;
    private readonly string penId;
    private PenSettings settings;
    private DesktopSurface? surface;
    private bool exiting;
    private bool deleted;
    private bool editing;

    public MainWindow() : this(new PenSettingsStore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft", "pen.json")), "Default")
    {
    }

    internal MainWindow(PenSettingsStore store, string penId)
    {
        this.store = store;
        this.penId = penId;
        InitializeComponent();
        settings = store.Load();
        ApplySettings();
        ItemsReady = InitializeItemsAsync();
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveSettings(); };
        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();
        SourceInitialized += (_, _) =>
        {
            surface = new DesktopSurface(new WindowInteropHelper(this).Handle);
            shellTimer.Tick += (_, _) => RefreshSurface();
            shellTimer.Start();
            RefreshSurface();
        };
        Loaded += (_, _) =>
        {
            // StartupUri can show a window after SourceInitialized. Reapply persisted intent.
            if (!settings.IsVisible) Hide();
            RefreshSurface();
        };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            DisposeItems();
            shellTimer.Stop();
            geometryKeyTimer.Stop();
            saveTimer.Stop();
            if (!deleted) SaveSettings();
            surface?.Dispose();
        };
    }

    internal Action<MainWindow>? DeleteRequested { get; set; }
    internal Action<MainWindow>? DeleteEmptyRequested { get; set; }
    internal Task ItemsReady { get; }
    internal event EventHandler? SettingsSaved;
    internal string PenTitle => settings.Title;
    internal bool UserVisible => settings.IsVisible;
    internal bool CanExit => !itemMovesBusy && !itemDragActive && !itemMenuBusy && !itemReconciliationBusy && !transferBusy && geometryGesture is null;

    internal void PlaceAt(System.Windows.Rect pixels)
    {
        Windows.DesktopGeometry.Place(this, pixels);
        SaveSettings();
    }

    private void ApplySettings()
    {
        Left = settings.Left;
        Top = settings.Top;
        Width = settings.Width;
        ApplyFoldedPresentation();
        TitleLabel.Text = settings.Title;
        ApplyBackground();
        ApplyItemScale();
    }

    private void ApplyBackground() => PenBody.Background = new SolidColorBrush(Color.FromArgb(
        (byte)Math.Round(settings.BackgroundOpacity * 255), 12, 10, 18));

    private void RefreshSurface() => surface?.Refresh(settings.IsVisible);

    private void ScheduleSave()
    {
        if (!IsLoaded || transferBusy || geometryGesture is not null) return;
        saveTimer.Stop();
        saveTimer.Start();
    }

    private bool SaveSettings()
    {
        if (transferBusy || deleted) return false;
        settings = TransferSnapshot();
        var saved = store.Save(settings);
        if (saved) SettingsSaved?.Invoke(this, EventArgs.Empty);
        return saved;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (itemMovesBusy || itemDragActive || itemMenuBusy || itemReconciliationBusy || transferBusy || geometryGesture is not null)
        {
            e.Cancel = true;
            exiting = false;
            ItemStatus.Text = "Wait for the current item operation to finish before exiting.";
            return;
        }
        if (exiting) return;
        e.Cancel = true;
        Dispatcher.InvokeAsync(RequestClose);
    }

    private void ClosePen(object sender, RoutedEventArgs e) => RequestClose();

    private void RequestClose()
    {
        if (deleted) return;
        FinishEditing(commit: true);
        if (!IsPortal && settings.ItemPaths.Length == 0 && DeleteEmptyRequested is { } remove)
        {
            remove(this);
            return;
        }
        HidePen();
    }

    internal void CompleteDeletion()
    {
        deleted = true;
        exiting = true;
        saveTimer.Stop();
        Close();
    }

    internal void HidePen()
    {
        if (deleted) return;
        FinishEditing(commit: true);
        settings = settings with { IsVisible = false };
        Hide();
        RefreshSurface();
        SaveSettings();
    }

    internal void ShowPen()
    {
        if (deleted) return;
        settings = settings with { IsVisible = true };
        Show();
        RefreshSurface();
        SaveSettings();
    }

    internal void ResetPosition()
    {
        if (deleted) return;
        geometryFallback = null;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 40;
        Top = workArea.Top + 40;
        Width = Math.Min(600, workArea.Width);
        settings = settings with { Height = Math.Min(300, workArea.Height) };
        ApplyFoldedPresentation();
        ShowPen();
    }

    internal void ExitApplication()
    {
        exiting = true;
        FinishEditing(commit: true);
        Close();
    }

    private void MovePen(object sender, DragDeltaEventArgs e) => UpdateGeometryDrag();

    private void ResizePen(object sender, DragDeltaEventArgs e) => UpdateGeometryDrag();


    private void BeginEditing()
    {
        editing = true;
        surface?.SetEditing(true);
        TitleEditor.Text = settings.Title;
        TitleLabel.Visibility = Visibility.Collapsed;
        TitleEditor.Visibility = Visibility.Visible;
        Activate(); // Keyboard focus is opt-in, only during explicit rename.
        TitleEditor.Focus();
        TitleEditor.SelectAll();
    }

    private void TitleEditorKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;
        FinishEditing(e.Key == Key.Enter);
        e.Handled = true;
    }

    private void TitleEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e) => FinishEditing(commit: true);

    private void FinishEditing(bool commit)
    {
        if (!editing) return;
        editing = false;
        if (commit) settings = (settings with { Title = TitleEditor.Text }).Normalize();
        TitleLabel.Text = settings.Title;
        TitleEditor.Visibility = Visibility.Collapsed;
        TitleLabel.Visibility = Visibility.Visible;
        surface?.SetEditing(false);
        SaveSettings();
    }
}
