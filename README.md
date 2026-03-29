# Ardimedia.VsExtensions.Common

[![NuGet](https://img.shields.io/nuget/v/Ardimedia.VsExtensions.Common.svg)](https://www.nuget.org/packages/Ardimedia.VsExtensions.Common)

Shared infrastructure for Ardimedia Visual Studio extensions built on the [VisualStudio.Extensibility](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/) SDK (out-of-process).

**NuGet:** https://www.nuget.org/packages/Ardimedia.VsExtensions.Common

## What's Included

### VS Theme Resources (XAML)

Pre-built control styles that adapt to VS Light, Dark, Blue, and High Contrast themes:

- **Themed Button** -- default button with hover/press states
- **ChromelessButton** -- borderless button for tabs and inline actions
- **Themed ComboBox** -- full template with dropdown arrow and popup
- **Themed ComboBoxItem** -- hover highlight in dropdown
- **Themed TextBox** -- text input with caret color
- **SelectableText** -- read-only text that supports copy (looks like TextBlock)

### ToolWindowViewModelBase (C#)

Base ViewModel class for tool windows with:

- **Solution monitoring** -- detects solution open/close/switch with two-pass debounce (waits for all projects to load)
- **IsScanning state** -- with `AnalyseButtonVisibility` / `CancelButtonVisibility` for separate Analyse and Cancel buttons
- **CancellationToken management** -- `_scanCts` pattern for cancellable scans
- **Virtual hooks** -- `OnSolutionOpenedAsync()`, `OnSolutionClosed()`, `OnIsScanningChanged()`

### OutputChannelLogger (C#)

Fire-and-forget wrapper for VS Output Window:

- Lazy channel creation
- `WriteLine(string message)` -- writes to a dedicated output channel

## Installation

```
dotnet add package Ardimedia.VsExtensions.Common
```

## Usage

### XAML Themes

In your tool window XAML, reference the theme resources:

```xaml
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <Grid>
        <Grid.Resources>
            <ResourceDictionary>
                <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="...path.../VsThemeResources.xaml" />
                </ResourceDictionary.MergedDictionaries>
            </ResourceDictionary>
        </Grid.Resources>
        <!-- Your content here -->
    </Grid>
</DataTemplate>
```

### ViewModel Base Class

```csharp
[DataContract]
public class MyToolWindowViewModel : ToolWindowViewModelBase
{
    public MyToolWindowViewModel(VisualStudioExtensibility extensibility)
        : base(extensibility) { }

    protected override async Task OnSolutionOpenedAsync(CancellationToken ct)
    {
        // Your scan logic here
    }

    protected override void OnSolutionClosed()
    {
        // Clear your data here
    }
}
```

### Output Logger

```csharp
var logger = new OutputChannelLogger(extensibility, "My Extension");
logger.WriteLine("Scan started...");
```

## Requirements

- .NET 10
- Microsoft.VisualStudio.Extensibility.Sdk 17.14+
- Visual Studio 2022 17.14+ or Visual Studio 2026

## License

MIT
