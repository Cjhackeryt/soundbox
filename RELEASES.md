# Release Notes & Distribution

This document outlines release packages, distribution channels, and installation procedures for **SoundBox**.

---

## Release Channels

| Channel | Target Audience | Compatibility | Recommended Source |
| :--- | :--- | :--- | :--- |
| **Stable / Beta** | General Macro Deck 3 users | Macro Deck `>= 3.0.0-beta.11` | Macro Deck Extension Store |
| **GitHub Releases** | Developers, early testers | Windows 10/11 x64 | `.macroDeckPlugin` release assets |

---

## Release History

### Version 0.1.1 (2026-09-22)

- **Package ID**: `com.cjhackeryt.soundbox`
- **Minimum Macro Deck Version**: `3.0.0-beta.11`
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
- **Minimum Macro Deck Version**: `3.0.0-beta.11`
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
