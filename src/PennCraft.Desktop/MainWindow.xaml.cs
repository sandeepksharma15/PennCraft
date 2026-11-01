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
    private readonly PenSettingsStore store = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft", "pen.json"));
    private readonly TrayCommands tray;
    private PenSettings settings;
    private DesktopSurface? surface;
    private bool exiting;
    private bool editing;

    public MainWindow()
    {
        InitializeComponent();
        settings = store.Load();
        ApplySettings();
        tray = new TrayCommands(Dispatcher, ShowPen, HidePen, ResetPosition, ExitApplication);
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
            shellTimer.Stop();
            saveTimer.Stop();
            SaveSettings();
            tray.Dispose();
            surface?.Dispose();
        };
    }

    private void ApplySettings()
    {
        Left = settings.Left;
        Top = settings.Top;
        Width = settings.Width;
        Height = settings.Height;
        TitleLabel.Text = settings.Title;
        ApplyBackground();
    }

    private void ApplyBackground() => PenBody.Background = new SolidColorBrush(Color.FromArgb(
        (byte)Math.Round(settings.BackgroundOpacity * 255), 12, 10, 18));

    private void RefreshSurface() => surface?.Refresh(settings.IsVisible);

    private void ScheduleSave()
    {
        if (!IsLoaded) return;
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void SaveSettings()
    {
        settings = settings with { Left = Left, Top = Top, Width = Width, Height = Height };
        store.Save(settings);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (exiting) return;
        e.Cancel = true;
        HidePen();
    }

    private void ClosePen(object sender, RoutedEventArgs e) => HidePen();

    private void HidePen()
    {
        FinishEditing(commit: true);
        settings = settings with { IsVisible = false };
        Hide();
        RefreshSurface();
        SaveSettings();
    }

    private void ShowPen()
    {
        settings = settings with { IsVisible = true };
        Show();
        RefreshSurface();
        SaveSettings();
    }

    private void ResetPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 40;
        Top = workArea.Top + 40;
        Width = Math.Min(600, workArea.Width);
        Height = Math.Min(300, workArea.Height);
        ShowPen();
    }

    private void ExitApplication()
    {
        exiting = true;
        FinishEditing(commit: true);
        Close();
    }

    private void MovePen(object sender, DragDeltaEventArgs e)
    {
        Left += e.HorizontalChange;
        Top += e.VerticalChange;
    }

    private void ResizePen(object sender, DragDeltaEventArgs e)
    {
        var bounds = settings with { Left = Left, Top = Top, Width = Width, Height = Height };
        var resized = PenGeometry.Resize(bounds, (string)((Thumb)sender).Tag,
            e.HorizontalChange, e.VerticalChange);
        Left = resized.Left;
        Top = resized.Top;
        Width = resized.Width;
        Height = resized.Height;
    }

    private void EditTitle(object sender, MouseButtonEventArgs e) => BeginEditing();

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

    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var rename = new MenuItem { Header = "Rename Pen" };
        rename.Click += (_, _) => Dispatcher.InvokeAsync(BeginEditing);
        menu.Items.Add(rename);
        var opacity = new MenuItem { Header = "Background opacity" };
        foreach (var value in new[] { 0.35, 0.55, 0.78, 1.0 })
        {
            var item = new MenuItem
            {
                Header = $"{value:P0}",
                IsCheckable = true,
                IsChecked = Math.Abs(settings.BackgroundOpacity - value) < 0.01
            };
            item.Click += (_, _) =>
            {
                settings = settings with { BackgroundOpacity = value };
                ApplyBackground();
                SaveSettings();
            };
            opacity.Items.Add(item);
        }
        menu.Items.Add(opacity);
        var hide = new MenuItem { Header = "Hide Pen" };
        hide.Click += (_, _) => HidePen();
        menu.Items.Add(hide);
        menu.PlacementTarget = (System.Windows.Controls.Button)sender;
        menu.IsOpen = true;
    }
}
