namespace PennCraft.Desktop.Windows;

// Kept independent of HWND access so fail-closed decisions can be tested.
public static class DesktopPolicy
{
    public static bool ShouldShow(bool shellAvailable, bool foregroundIsShellDesktop) =>
        shellAvailable && foregroundIsShellDesktop;
}
