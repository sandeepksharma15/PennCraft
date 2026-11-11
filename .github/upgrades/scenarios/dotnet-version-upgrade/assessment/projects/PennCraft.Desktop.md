# src\PennCraft.Desktop\PennCraft.Desktop.csproj

[← Back to the assessment index](../../assessment.md)

## Project Info

- **Current Target Framework:** net10.0-windows
- **Proposed Target Framework:** net11.0-windows
- **SDK-style**: True
- **Project Kind:** Wpf
- **Dependencies**: 0
- **Dependants**: 1
- **Number of Files**: 74
- **Number of Files with Incidents**: 45
- **Lines of Code**: 6304
- **Estimated LOC to modify**: 3155+ (at least 50.0% of the project)

## Related Projects

**Depended on by (1)** — projects that reference this one:

- [F:\Projects\PennCraft\tests\PennCraft.Desktop.Tests\PennCraft.Desktop.Tests.csproj](../projects/PennCraft.Desktop.Tests.md)

## Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph upstream["Dependants (1)"]
        P2["<b>📦&nbsp;PennCraft.Desktop.Tests.csproj</b><br/><small>net10.0-windows</small>"]
        click P2 "../projects/PennCraft.Desktop.Tests.md"
    end
    subgraph current["PennCraft.Desktop.csproj"]
        MAIN["<b>📦&nbsp;PennCraft.Desktop.csproj</b><br/><small>net10.0-windows</small>"]
        click MAIN "../projects/PennCraft.Desktop.md"
    end
    P2 --> MAIN

```

## API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 3130 | High - Require code changes |
| 🟡 Source Incompatible | 12 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 13 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 17521 |  |
| ***Total APIs Analyzed*** | ***20676*** |  |

## Project Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |
| Windows Forms Legacy Controls | 2 | 0.1% | Legacy Windows Forms controls that have been removed from .NET Core/5+ including StatusBar, DataGrid, ContextMenu, MainMenu, MenuItem, and ToolBar. These controls were replaced by more modern alternatives. Use ToolStrip, MenuStrip, ContextMenuStrip, and DataGridView instead. |
| GDI+ / System.Drawing | 12 | 0.4% | System.Drawing APIs for 2D graphics, imaging, and printing that are available via NuGet package System.Drawing.Common. Note: Not recommended for server scenarios due to Windows dependencies; consider cross-platform alternatives like SkiaSharp or ImageSharp for new code. |
| Windows Forms | 226 | 7.2% | Windows Forms APIs for building Windows desktop applications with traditional Forms-based UI that are available in .NET on Windows. Enable Windows Desktop support: Option 1 (Recommended): Target net9.0-windows; Option 2: Add <UseWindowsDesktop>true</UseWindowsDesktop>; Option 3 (Legacy): Use Microsoft.NET.Sdk.WindowsDesktop SDK. |
| WPF (Windows Presentation Foundation) | 1363 | 43.2% | WPF APIs for building Windows desktop applications with XAML-based UI that are available in .NET on Windows. WPF provides rich desktop UI capabilities with data binding and styling. Enable Windows Desktop support: Option 1 (Recommended): Target net9.0-windows; Option 2: Add <UseWindowsDesktop>true</UseWindowsDesktop>. |

