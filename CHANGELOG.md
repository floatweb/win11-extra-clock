# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-09-05
### Added
- Clock flyout with **seconds** and **long date**, aligned with the Windows calendar.
- **Tray icon** (light/dark) with **red update badge** when a new version is available.
- Tray menu entry that shows when **“Update available”**.
- **Test mode**: open the flyout without the Windows calendar (handy for first run, positioning, and screenshots).
- Position presets (Top-Left/Center/Right, Bottom-Left/Center/Right) and **Custom** offset.
- **Update service**: startup + periodic checks (ETag/304, tolerant parser, optional GitHub Releases fallback).
- **Localization** infrastructure; runtime language switching for UI/menu/tooltips.
- **Precise second timer** to avoid jitter/skipped seconds.

### Technical
- WPF desktop app targeting **Windows 11**  
  `<SupportedOSPlatformVersion>10.0.22000.0</SupportedOSPlatformVersion>`
- Optimized resource usage (disposal of NotifyIcon/icons/timers, single updater instance).
- Minimal logs; per-user update log at `%LOCALAPPDATA%\Win11ExtraClock\update.log`.

[1.0.0]: https://github.com/floatweb/win11-extra-clock/releases/tag/v1.0.0
