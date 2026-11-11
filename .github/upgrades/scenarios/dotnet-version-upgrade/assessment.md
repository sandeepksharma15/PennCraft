# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v11.0.

Detailed findings live alongside this file in `assessment/`. This page is the index: read it first, then open only the documents you need.

## Table of Contents

- [Executive Summary](#executive-summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Detailed Reports](#detailed-reports)
  - [Projects Relationship Graph](assessment/project-graph.md)
  - [Aggregate NuGet packages details](assessment/nuget/aggregate-packages.md)
  - [Most Frequent API Issues (complete list)](assessment/api-issues/most-frequent-api-issues.md)
  - [Project Details](#project-details)

## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 2 | All require upgrade |
| Total NuGet Packages | 17 | All compatible |
| Total Code Files | 108 |  |
| Total Code Files with Incidents | 64 |  |
| Total Lines of Code | 10041 |  |
| Total Number of Issues | 3829 |  |
| Proposed Target Framework | net11.0--windows, net11.0-windows |  |
| Estimated LOC to modify | 3827+ | at least 38.1% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Binding Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| [src\PennCraft.Desktop\PennCraft.Desktop.csproj](assessment/projects/PennCraft.Desktop.md) | net10.0-windows | 🟡 Medium | 0 | 3155 | 0 | 3155+ | Wpf, Sdk Style = True |
| [tests\PennCraft.Desktop.Tests\PennCraft.Desktop.Tests.csproj](assessment/projects/PennCraft.Desktop.Tests.md) | net10.0-windows | 🟡 Medium | 0 | 672 | 0 | 672+ | DotNetCoreApp, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 17 | 100.0% |
| ⚠️ Incompatible | 0 | 0.0% |
| 🔄 Upgrade Recommended | 0 | 0.0% |
| ***Total NuGet Packages*** | ***17*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 3794 | High - Require code changes |
| 🟡 Source Incompatible | 18 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 15 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 28422 |  |
| ***Total APIs Analyzed*** | ***32249*** |  |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |
| WPF (Windows Presentation Foundation) | 1523 | 39.8% | WPF APIs for building Windows desktop applications with XAML-based UI that are available in .NET on Windows. WPF provides rich desktop UI capabilities with data binding and styling. Enable Windows Desktop support: Option 1 (Recommended): Target net9.0-windows; Option 2: Add <UseWindowsDesktop>true</UseWindowsDesktop>. |
| Windows Forms | 226 | 5.9% | Windows Forms APIs for building Windows desktop applications with traditional Forms-based UI that are available in .NET on Windows. Enable Windows Desktop support: Option 1 (Recommended): Target net9.0-windows; Option 2: Add <UseWindowsDesktop>true</UseWindowsDesktop>; Option 3 (Legacy): Use Microsoft.NET.Sdk.WindowsDesktop SDK. |
| GDI+ / System.Drawing | 18 | 0.5% | System.Drawing APIs for 2D graphics, imaging, and printing that are available via NuGet package System.Drawing.Common. Note: Not recommended for server scenarios due to Windows dependencies; consider cross-platform alternatives like SkiaSharp or ImageSharp for new code. |
| Windows Forms Legacy Controls | 2 | 0.1% | Legacy Windows Forms controls that have been removed from .NET Core/5+ including StatusBar, DataGrid, ContextMenu, MainMenu, MenuItem, and ToolBar. These controls were replaced by more modern alternatives. Use ToolStrip, MenuStrip, ContextMenuStrip, and DataGridView instead. |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |
| T:System.Windows.Rect | 245 | 6.4% | Binary Incompatible |
| T:System.Windows.Point | 118 | 3.1% | Binary Incompatible |
| T:System.Windows.DragDropEffects | 89 | 2.3% | Binary Incompatible |
| T:System.Windows.Thickness | 85 | 2.2% | Binary Incompatible |
| T:System.Windows.Controls.TextBlock | 77 | 2.0% | Binary Incompatible |
| M:System.Windows.Rect.#ctor(System.Double,System.Double,System.Double,System.Double) | 76 | 2.0% | Binary Incompatible |
| T:System.Windows.Controls.MenuItem | 72 | 1.9% | Binary Incompatible |
| P:System.Windows.Controls.TextBlock.Text | 65 | 1.7% | Binary Incompatible |
| T:System.Windows.MessageBoxResult | 56 | 1.5% | Binary Incompatible |
| T:System.Windows.Threading.DispatcherTimer | 56 | 1.5% | Binary Incompatible |

The table above is the top 10. See [the complete list](assessment/api-issues/most-frequent-api-issues.md) for every affected API.

## Detailed Reports

- [Projects Relationship Graph](assessment/project-graph.md)
- [Aggregate NuGet packages details](assessment/nuget/aggregate-packages.md)
- [Most Frequent API Issues (complete list)](assessment/api-issues/most-frequent-api-issues.md)

### Project Details

- [src\PennCraft.Desktop\PennCraft.Desktop.csproj](assessment/projects/PennCraft.Desktop.md)
- [tests\PennCraft.Desktop.Tests\PennCraft.Desktop.Tests.csproj](assessment/projects/PennCraft.Desktop.Tests.md)


