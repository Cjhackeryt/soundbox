# SoundBox

A high-performance Windows soundboard plugin for **Macro Deck 3**.

[![Macro Deck 3](https://img.shields.io/badge/Macro%20Deck-3.0.0--beta.11+-blue.svg)](https://macro-deck.app)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-brightgreen.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Publisher](https://img.shields.io/badge/Publisher-CJHackerYT-orange.svg)]()

---

## Overview

**SoundBox** (`com.cjhackeryt.soundbox`) turns your Macro Deck 3 setup into a dedicated soundboard. Trigger audio clips, sound effects, background loops, and memes from any Macro Deck client (Android, iOS, or Web) directly to chosen Windows audio outputs.

With built-in WASAPI low-latency rendering and dual-output audio monitoring, you can route sound effects directly into virtual microphones (e.g., VB-Cable, Voicemeeter) for Discord, OBS, or game chat while simultaneously monitoring the sound through your own headphones.

---

## Features

- 🎵 **Broad Audio Support**: Plays standard uncompressed `.wav` and compressed `.mp3` audio files.
- 🎛️ **Targeted Device Routing**: Send audio to any active Windows audio playback device or virtual audio cable.
- 🎧 **Dual-Output Monitoring**: Concurrently echo the sound to your default Windows playback device (e.g., headset/speakers) so you always hear what you play.
- 🔊 **Fine-Grained Volume**: Dedicated volume slider (0% to 100%) for custom sound balancing.
- 🔁 **Continuous Looping**: Repeat sounds indefinitely until manually stopped.
- ⏹️ **Instant Stop Control**: Dedicated "Stop Sound" action to immediately halt playback.
- ⚡ **Low-Latency WASAPI Engine**: Powered by NAudio and Windows Core Audio APIs in shared mode for glitch-free, responsive playback without blocking the Macro Deck host.

---

## System Requirements

- **Operating System**: Windows 10 / Windows 11 (x64)
- **Macro Deck**: Macro Deck 3 (`>= 3.0.0-beta.11`)
- **Audio Output**: Any active Windows audio playback endpoint (Realtek, USB DAC, VB-Cable, Voicemeeter, etc.)

---

## Available Actions

### 1. Play Sound (`play-sound`)
Plays a sound file through the selected Windows audio output device.

| Parameter | Type | Required | Description | Default |
| :--- | :--- | :--- | :--- | :--- |
| **Sound File** | File Picker (`.wav`, `.mp3`) | Yes | Path to the local sound file. | — |
| **Output Device** | Dynamic Dropdown | Yes | Selects the Windows output device or default device. | Windows Default Playback Device |
| **Monitor Sound** | Toggle | No | Also plays sound through the default Windows playback device. | `true` |
| **Volume** | Slider (0–100%) | No | Adjusts playback volume. | `100` |
| **Loop** | Toggle | No | Automatically restarts playback when the file reaches the end. | `false` |

### 2. Stop Sound (`stop-sound`)
Immediately stops any sound currently being played by SoundBox across all output devices.

---

## Setup & Usage Guide

### Routing Sound to Discord / OBS + Monitoring Locally

1. Install a virtual audio cable such as [VB-CABLE](https://vb-audio.com/Cable/) or Voicemeeter.
2. Open **Macro Deck 3** on your PC.
3. Edit any button on your deck and add the **Play Sound** action:
   - **Sound File**: Browse and select your `.wav` or `.mp3` file.
   - **Output Device**: Select **CABLE Input (VB-Audio Virtual Cable)**.
   - **Monitor Sound**: Set to **Enabled (ON)**.
   - **Volume**: Adjust as desired.
4. In Discord or OBS:
   - Set the Input Device (Microphone) to **CABLE Output (VB-Audio Virtual Cable)**.
5. Press the button on your phone, tablet, or stream deck.
   - Your voice chat / stream hears the sound via the virtual cable.
   - You hear the sound in your headphones via the monitor channel.

---

## Building from Source

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows x64 development environment
- [Macro Deck Plugin CLI](https://www.nuget.org/packages/MacroDeck.Plugin.Cli) (`dotnet tool install -g MacroDeck.Plugin.Cli`)

### Build Steps

1. Clone or download the repository:
   ```bash
   git clone https://github.com/cjhackeryt/soundbox.git
   cd soundbox
   ```

2. Restore packages and compile:
   ```bash
   dotnet build
   ```

3. Run automated tests:
   ```bash
   dotnet test
   ```

4. Build and package the plugin (.NET 10 framework-dependent artifact):
   ```bash
   macrodeck-plugin build --output ./artifacts
   ```

5. (Optional) Run the Macro Deck conformance test suite:
   ```bash
   macrodeck-plugin test --artifact ./artifacts/com.cjhackeryt.soundbox-0.1.3.macroDeckPlugin
   ```

---

## Plugin Information

- **Plugin ID**: `com.cjhackeryt.soundbox`
- **Publisher**: `CJHackerYT`
- **Version**: `0.1.3`
- **License**: [MIT](LICENSE)
