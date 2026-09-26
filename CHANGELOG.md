# Changelog

All notable changes to the **SoundBox** plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned
- Global hotkey support for instant sound triggers.
- Support for playback speed / pitch adjustments.
- Multi-channel simultaneous playback (sound layering).

## [1.0.7] - 2026-09-27

### Added
- Play Sound now counts down on its own widget while the sound plays, and returns the widget to its configured text and icon when playback ends. No variable, widget or extra configuration is needed.
- Each Play Sound action instance drives its own countdown from its own `OwnerWidgetId`, so several sound buttons stay independent and each one shows the duration of the sound configured on it.

### Changed
- Marked the `soundbox_playback_remaining` variable as deprecated in its description. It still works for configurations that already reference it, but the widget countdown no longer depends on it.

## [1.0.6] - 2026-09-27

### Added
- New Macro Deck text variable `soundbox_playback_remaining` showing the time left in the currently playing sound as `MM:SS` (widening to `HH:MM:SS` past an hour).
- The variable is derived from the audio engine's live playback position, so it stays in step with what is being heard, follows loop restarts, and reads `00:00` whenever nothing is playing.
- The variable refreshes roughly every 300 ms while it is bound, with no additional timer or background thread in the plugin.
- Automated tests that play real audio through a Windows output device and assert the countdown, plus generated silent WAV fixtures so the suite needs no binary asset.

### Changed
- Upgraded the Macro Deck SDK from `3.0.0-beta.12` to `3.0.0-beta.14` and pinned the release workflow's Macro Deck Plugin CLI to the same version. The plugin's `IVariableProvider` contract and the negotiated protocol range are unchanged between the two releases, so required host compatibility stays at `>= 3.0.0-beta.12`.

## [1.0.5] - 2026-09-23

### Changed
- Published the stable 1.0.5 release.

## [0.1.6] - 2026-09-23

### Documentation
- Added release documentation for the Macro Deck SDK `3.0.0-beta.12` upgrade.

## [0.1.4] - 2026-09-23

### Changed
- Aligned the release workflow CLI with the Macro Deck SDK version.

## [0.1.3] - 2026-09-23

### Fixed
- Pinned the Macro Deck SDK to the Store-supported minimum version.

## [0.1.2] - 2026-09-23

### Fixed
- Avoided initializing Windows audio devices on non-Windows platforms.
- Reported unsupported platforms cleanly instead of failing during audio manager construction.

## [0.1.1] - 2026-09-22

### Changed
- Migrated plugin to framework-dependent .NET 10 deployment for Macro Deck 3 host runtime.
- Reduced packed plugin artifact size from ~307 MB to ~1.5 MB.
- Removed remote `$schema` URL from `manifest.json` to resolve IDE parsing warnings.

### Added
- Complete localization infrastructure (`Localization/Strings.resx`) with fully localized actions, parameters, and error codes.
- Automated unit test suite (`SoundBox.Tests`) covering action configuration, catalog discovery, execution validation, and localization.
- Centralized project solution (`SoundBox.sln`).

---

## [0.1.0] - 2026-09-22

### Added
- Initial release of **SoundBox** for Macro Deck 3 (`com.cjhackeryt.soundbox`).
- Compatibility with **Macro Deck 3.0.0-beta.11** and later.
- Core `play-sound` action:
  - File picker with support for `.wav` and `.mp3` audio files.
  - Dynamic dropdown for selecting active Windows WASAPI audio render devices.
  - Built-in "Windows Default Playback Device" option.
  - Audio monitoring toggle to simultaneously mirror playback through the default Windows device.
  - Volume slider with range from 0% to 100%.
  - Looping toggle for automatic sound repeat.
- Core `stop-sound` action to immediately halt active playback across all output endpoints.
- Low-latency audio backend powered by NAudio and Windows Core Audio (WASAPI shared mode).
- Asynchronous session cleanup and background disposal to prevent deadlocks on audio threads.
- Comprehensive documentation (`README.md`, `CHANGELOG.md`, `RELEASES.md`, `AGENTS.md`).
- Framework-dependent Windows x64 packaging matching Macro Deck 3 standards.
- Full localization infrastructure (`Localization/Strings.resx`) for all actions, parameters, dynamic choices, and error messages.
- Comprehensive unit test suite (`SoundBox.Tests`) validating actions, catalog wiring, and execution error handling.
