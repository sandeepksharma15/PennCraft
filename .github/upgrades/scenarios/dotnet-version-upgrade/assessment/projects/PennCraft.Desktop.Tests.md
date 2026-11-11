# tests\PennCraft.Desktop.Tests\PennCraft.Desktop.Tests.csproj

[← Back to the assessment index](../../assessment.md)

## Project Info

- **Current Target Framework:** net10.0-windows
- **Proposed Target Framework:** net11.0--windows
- **SDK-style**: True
- **Project Kind:** DotNetCoreApp
- **Dependencies**: 1
- **Dependants**: 0
- **Number of Files**: 35
- **Number of Files with Incidents**: 19
- **Lines of Code**: 3737
- **Estimated LOC to modify**: 672+ (at least 18.0% of the project)

## Related Projects

**Depends on (1)** — projects this one references:

- [F:\Projects\PennCraft\src\PennCraft.Desktop\PennCraft.Desktop.csproj](../projects/PennCraft.Desktop.md)

## Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["PennCraft.Desktop.Tests.csproj"]
        MAIN["<b>📦&nbsp;PennCraft.Desktop.Tests.csproj</b><br/><small>net10.0-windows</small>"]
        click MAIN "../projects/PennCraft.Desktop.Tests.md"
    end
    subgraph downstream["Dependencies (1)"]
        P1["<b>📦&nbsp;PennCraft.Desktop.csproj</b><br/><small>net10.0-windows</small>"]
        click P1 "../projects/PennCraft.Desktop.md"
    end
    MAIN --> P1

```

## API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 664 | High - Require code changes |
| 🟡 Source Incompatible | 6 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 2 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 10901 |  |
| ***Total APIs Analyzed*** | ***11573*** |  |

## Project Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |
| WPF (Windows Presentation Foundation) | 160 | 23.8% | WPF APIs for building Windows desktop applications with XAML-based UI that are available in .NET on Windows. WPF provides rich desktop UI capabilities with data binding and styling. Enable Windows Desktop support: Option 1 (Recommended): Target net9.0-windows; Option 2: Add <UseWindowsDesktop>true</UseWindowsDesktop>. |
| GDI+ / System.Drawing | 6 | 0.9% | System.Drawing APIs for 2D graphics, imaging, and printing that are available via NuGet package System.Drawing.Common. Note: Not recommended for server scenarios due to Windows dependencies; consider cross-platform alternatives like SkiaSharp or ImageSharp for new code. |

