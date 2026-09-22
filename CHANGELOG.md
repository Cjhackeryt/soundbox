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
