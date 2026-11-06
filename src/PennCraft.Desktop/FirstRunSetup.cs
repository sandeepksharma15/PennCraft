using System.IO;

namespace PennCraft.Desktop;

/// <summary>Declining setup creates an empty workspace before the normal startup path can scan or move files.</summary>
internal static class FirstRunSetup
{
    internal static void StartEmpty(WorkspaceStore workspace)
    {
        if (!workspace.IsUninitialized) return;
        if (!new PenSettingsStore(workspace.SettingsPath("Default")).Save(new PenSettings { Title = "New Pen" }))
            throw new IOException("The initial Pen settings could not be saved.");
        workspace.Save(new WorkspaceSettings(1, ["Default"], OrganizationOfferDismissed: true));
    }
}
