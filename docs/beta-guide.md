# PennCraft beta guide

PennCraft organizes desktop files, shortcuts and folders into Pens and live Folder Portals. This is the first public beta, not a stable release. Start with backed-up files and report problems with your Windows and PennCraft versions.

## Download and run

Supported target: Windows 11 x64. ARM64 and Windows 10 are not validated. Both packages include .NET; you do not need to install it separately.

Download from https://github.com/sandeepksharma15/PennCraft/releases/tag/v0.1.0-beta.1:
- **Installer:** run the file ending in `-setup.exe`. It installs for your account, adds a Start menu entry and offers an optional desktop shortcut.
- **Portable:** extract the entire `-portable.zip` into a new folder, then run `PennCraft.exe`. Keep its DLLs and other files together. Do not run it inside the ZIP.

Neither package adds automatic startup. One instance can run for your Windows account. Installer and portable builds share that account's workspace; portable means no installation, not a separate USB-carried data store.

The beta is unsigned. Windows may show an unknown-publisher or reputation warning. Verify the official download and checksum and follow your organization's policy. A checksum verifies integrity; it is not a digital signature or guarantee of safety.

Compare a download with SHA256SUMS.txt:
```powershell
Get-FileHash .\PennCraft-0.1.0-beta.1-win-x64-setup.exe -Algorithm SHA256
```

## First launch

Choose whether to organize the desktop:
- **Yes:** physically move desktop PDFs into Documents, shortcuts into Programs and folders into Folders. Other items remain in place.
- **No:** start with an empty Pen.
- **Cancel:** exit without organizing.

Shared desktop shortcuts still require a confirmed move and may cause Windows to request permission. Existing installations keep their workspace and are not automatically reorganized.

Find the blue PennCraft icon in the notification area, including hidden tray icons. Its menu shows hidden Pens, resets positions, saves/restores layouts, opens About and exits. Closing an empty ordinary Pen deletes that empty container; other closes hide the container.

## Where your data lives

Settings, saved layouts, recovery records and logs: `%LOCALAPPDATA%\PennCraft`.

Physically moved desktop items: the hidden **.PennCraft** folder on your actual Windows desktop, which may be redirected to OneDrive. These are real user files. Do not delete this folder as a cache.

Folder Portals keep files in their linked folders. Saved layouts capture presentation; they do not back up files or restore old memberships.

## Update, backup and uninstall

1. Exit PennCraft through the tray and let file operations finish.
2. Back up both the settings folder and the desktop's .PennCraft folder together. Back up linked folders separately as needed.
3. Install the new version, or extract a new portable ZIP into a fresh folder. Do not mix old and new runtime DLLs.
4. Run PennCraft; it reuses settings and reconciles pending recovery records.

The installer asks you to exit a running PennCraft before update or uninstall. It does not force-close file operations. Uninstall removes installed program files and shortcuts while retaining settings and managed desktop files. Before uninstalling permanently, move items back through PennCraft or keep a verified backup. Removing a portable application folder also leaves user data separate.

To restore a backup, exit first and restore settings and managed storage as a matched pair. Keep a copy of the current state before replacing anything. Do not delete recovery journals to clear an error.

## Limitations and support

- Desktop placement can vary after Explorer restarts, Win+D, display changes and virtual desktops.
- Saved layouts require matching monitor topology, work areas and display scaling.
- Multi-selection, tabs, workspace pages, monitor profiles and automatic updates are not included.
- Shell extensions and folder providers may differ from ordinary local files.
- Automated package checks do not replace manual Windows 11 desktop testing.

For missing Pens, use **Show all Pens** or **Reset position and show** from the tray. Unexpected errors are logged locally and the app closes so restart recovery can run. About shows the version and opens settings/logs.

Report bugs at https://github.com/sandeepksharma15/PennCraft/issues/new/choose. Include steps, expected/actual results, Windows build, scaling and app version. Review logs before sharing: they can contain paths and exception details. Do not upload entire settings/storage folders or private files. PennCraft has no built-in telemetry or silent update downloads.

Deployment guidance: https://learn.microsoft.com/en-us/dotnet/core/deploying/
