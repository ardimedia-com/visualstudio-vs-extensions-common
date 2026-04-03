# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-04-03

### Added

- Configurable timing parameters for `ToolWindowViewModelBase` solution monitoring:
  - `InitialDelayMs` (default: 500ms) — delay before first scan
  - `PollIntervalMs` (default: 3000ms) — interval between fingerprint checks
  - `DebounceIntervalMs` (default: 5000ms) — interval between stability checks
  - `StableReadingsRequired` (default: 2) — number of stable readings before scan

### Notes

- Fully backward-compatible: existing extensions use the same defaults as before
- Extensions that need faster detection (e.g., sidebar tool windows) can override these values

## [1.0.0] - 2026-03-29

### Added

- `ToolWindowViewModelBase` — abstract base class for VS tool window ViewModels
  - Solution monitoring with two-pass debounce
  - IsScanning state with Analyse/Cancel button visibility
  - CancellationTokenSource management
  - Solution fingerprint tracking
- `OutputChannelLogger` — fire-and-forget VS Output Window logging
- `VsThemeResources.xaml` — WPF theming for dark/light/blue modes
