namespace PennCraft.Desktop.Windows;

// User visibility survives temporary shell loss and native Show Desktop transitions.
public static class DesktopPolicy
{
    public static bool ShouldShow(bool shellAvailable, bool userVisible) => shellAvailable && userVisible;
}
