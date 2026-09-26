# Release Notes & Distribution

This document outlines release packages, distribution channels, and installation procedures for **SoundBox**.

---

## Release Channels

| Channel | Target Audience | Compatibility | Recommended Source |
| :--- | :--- | :--- | :--- |
| **Stable / Beta** | General Macro Deck 3 users | Macro Deck `>= 3.0.0-beta.12` | Macro Deck Extension Store |
| **GitHub Releases** | Developers, early testers | Windows 10/11 x64 | `.macroDeckPlugin` release assets |

---

## Release History

### Version 1.0.7 (2026-09-27)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0` (Framework-Dependent)

#### Highlights
- Play Sound shows a live countdown on the widget that was pressed, then restores that widget's configured text and icon when the sound ends. Nothing extra to configure.
- Countdowns are per action instance, so separate sound buttons remain independent and each shows the length of its own sound.
- The `soundbox_playback_remaining` variable is kept for existing configurations but is no longer required.

#### Artifacts
- `com.cjhackeryt.soundbox-1.0.7.macroDeckPlugin`


### Version 1.0.6 (2026-09-27)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0` (Framework-Dependent)

#### Highlights
- Added the Macro Deck text variable `soundbox_playback_remaining`, which shows the time left in the active playback as `MM:SS` (`HH:MM:SS` past an hour).
- The value comes from the audio engine's own playback position, so the countdown matches the audio, wraps on loop restarts, and reports `00:00` when idle, stopped, or finished.
- Built against Macro Deck SDK `3.0.0-beta.14` (up from `3.0.0-beta.12`). Required host compatibility is unchanged at `3.0.0-beta.12`.

#### Artifacts
- `com.cjhackeryt.soundbox-1.0.6.macroDeckPlugin`


### Version 1.0.5 (2026-09-23)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0` (Framework-Dependent)

#### Highlights
- Published the stable 1.0.5 release.

#### Artifacts
- `com.cjhackeryt.soundbox-1.0.5.macroDeckPlugin`


### Version 0.1.4 (2026-09-23)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0` (Framework-Dependent)

#### Highlights
- Aligned the release workflow CLI with the Macro Deck SDK version.

#### Artifacts
- `com.cjhackeryt.soundbox-0.1.4.macroDeckPlugin`


### Version 0.1.3 (2026-09-23)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0` (Framework-Dependent)

#### Highlights
- Pinned the Macro Deck SDK dependency to the Store-supported minimum version.

#### Artifacts
- `com.cjhackeryt.soundbox-0.1.3.macroDeckPlugin`

---

### Version 0.1.2 (2026-09-23)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0` (Framework-Dependent)

#### Highlights
- Fixed audio manager initialization so non-Windows test and inspection environments do not attempt to create Windows audio devices.
- Added a clear unsupported-platform guard for audio playback.

#### Artifacts
- `com.cjhackeryt.soundbox-0.1.2.macroDeckPlugin`

---

### Version 0.1.1 (2026-09-22)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0` (Framework-Dependent)

#### Highlights
- Standardized framework-dependent distribution packaging, reducing archive size from ~307 MB to ~1.5 MB.
- Full localization architecture via `Localization/Strings.resx` with typed resources for all actions, options, and error messages.
- Automated unit test suite (`SoundBox.Tests`) and centralized solution (`SoundBox.sln`).
- Cleaned manifest schema definitions.

#### Artifacts
- `com.cjhackeryt.soundbox-0.1.1.macroDeckPlugin`

---

### Version 0.1.0 (2026-09-22)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.12`
- **Supported Platforms**: `win-x64`
- **Target Framework**: `.NET 10.0`

#### Highlights
- First public release of the SoundBox soundboard plugin for Macro Deck 3.
- Full support for `.wav` and `.mp3` playback.
- Dual-channel monitoring: Play audio through a virtual audio cable for stream/voice chat and hear it simultaneously through your default headphones.
- Integrated dynamic device enumeration with WASAPI shared output mode.
- Volume control (0–100%) and automatic looping.
- Instant stop action for fast playback cutoff.
- Deadlock-free asynchronous audio thread lifecycle management.

#### Artifacts
- `com.cjhackeryt.soundbox-0.1.0-win-x64.macroDeckPlugin`
- Self-contained binary bundle for Windows x64.

---

## Manual Installation Guide

If installing manually without the Macro Deck Extension Store:

1. Download the latest `com.cjhackeryt.soundbox-*.macroDeckPlugin` from the Releases page.
2. Open **Macro Deck 3** on your PC.
3. Navigate to **Extensions** -> **Install from file**.
4. Select the downloaded `.macroDeckPlugin` file.
5. Macro Deck will unpack the runtime and register the plugin.
6. The `Play Sound` and `Stop Sound` actions will now be available in your action selector.

---

## Building a Release Package

To build and package a release for distribution:

```bash
# 1. Publish self-contained win-x64 binary
dotnet publish SoundBox.csproj -c Release -r win-x64 --self-contained true -o bin/publish/win-x64

# 2. Package using Macro Deck Plugin CLI (if installed)
macrodeck-plugin build --source . --version 0.1.0
```
