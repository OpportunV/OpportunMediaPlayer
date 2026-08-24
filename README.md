# OMP — Opportun Media Player

[![CI](https://github.com/OpportunV/OpportunMediaPlayer/actions/workflows/ci.yml/badge.svg)](https://github.com/OpportunV/OpportunMediaPlayer/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/OpportunV/OpportunMediaPlayer)](https://github.com/OpportunV/OpportunMediaPlayer/releases)
[![License: PolyForm Noncommercial 1.0.0](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-blue)](LICENSE)

A local media player built on [Avalonia](https://avaloniaui.net/) and FFmpeg. Its headline
feature: route different audio tracks from the same file to different audio output devices
simultaneously — for example, main audio to your speakers and a commentary track to a headset,
playing at the same time.

[opportunv.github.io/OpportunMediaPlayer](https://opportunv.github.io/OpportunMediaPlayer/)

> Source-available, not OSI open source: free for any noncommercial use, but not under a
> license like MIT/GPL. See [License](#license) below.

## Screenshots

<table>
<tr>
<td align="center" width="50%">
<a href=".github/images/main_window.png"><img src=".github/images/main_window.png" width="380" alt="Main window, dark theme, per-output volume flyout"></a>
<br><sub>Main window — dark theme, per-output volume flyout</sub>
</td>
<td align="center" width="50%">
<a href=".github/images/main_window_light_theme.png"><img src=".github/images/main_window_light_theme.png" width="380" alt="Main window, light theme, playback speed flyout"></a>
<br><sub>Main window — light theme, playback speed flyout</sub>
</td>
</tr>
<tr>
<td align="center" width="50%">
<a href=".github/images/options_audio_settings.png"><img src=".github/images/options_audio_settings.png" width="380" alt="Options window, Audio Settings tab, routing tracks to multiple outputs"></a>
<br><sub>Options → Audio Settings — routing tracks to multiple output devices</sub>
</td>
<td align="center" width="50%">
<a href=".github/images/subtitle_zone_editor.png"><img src=".github/images/subtitle_zone_editor.png" width="380" alt="Subtitle zone editor"></a>
<br><sub>Subtitle zone editor — position, font, and color per zone</sub>
</td>
</tr>
</table>

## Features

- **Multi-output audio routing** — assign any audio track to any output device; the same track
  can be sent to multiple devices at once, each with independent volume.
- **Per-output and master volume**, 0–200%, composed multiplicatively (each slider's feel stays
  independent of the other). A quick per-output volume popup sits right on the main toolbar, next
  to the full routing/delay controls in Options.
- **Playback speed control** — 0.5x–2x, YouTube-style presets, pitch-preserving (speeding up or
  slowing down doesn't change pitch).
- **Subtitles with positionable zones** — assign subtitle tracks to on-screen zones you define
  and style. Load an external subtitle file onto an already-open video from Options, on top of
  whatever the file/URL itself provides.
- **Open URL playback via [yt-dlp](https://github.com/yt-dlp/yt-dlp)** — paste a page URL
  (File → Open URL...) and OMP resolves and streams it. When a video exposes multiple audio dubs
  or caption tracks, they show up as ordinary routable audio/subtitle tracks, same as a local
  file's — including auto-generated captions, in the original language and (when different) the
  app's own UI language.
- **Light / dark / system theme**, applied consistently across the main window, popups, and
  dialogs.
- **In-app keyboard shortcut reference** (Help → Keyboard Shortcuts).

## Keyboard shortcuts

| Keys | Action |
|---|---|
| Space | Play / pause |
| Left Arrow | Step back |
| Right Arrow | Step forward |
| Up Arrow | Increase volume |
| Down Arrow | Decrease volume |
| M | Toggle mute |
| F | Toggle fullscreen |
| C | Toggle subtitles |
| Esc | Exit fullscreen |
| Shift + , | Decrease playback speed |
| Shift + . | Increase playback speed |

Double-clicking the video also toggles fullscreen. The same shortcut list is available from
Help → Keyboard Shortcuts inside the app.

## Platform support

- **Windows x64** — fully supported, native FFmpeg libraries bundled.
- **Linux x64** — native FFmpeg libraries bundled and CI builds/tests pass, but real-world testing
  so far has only been on a single Ubuntu 22.04/24.04 VirtualBox VM — broader distro and desktop
  environment coverage is unverified. If you try it on something else, a report via
  [Issues](https://github.com/OpportunV/OpportunMediaPlayer/issues) is welcome either way.
- **macOS (Apple Silicon and Intel)** — confirmed working on one Apple Silicon Mac so far — Intel
  and broader macOS version coverage is still unverified. Unlike Windows/Linux, FFmpeg isn't
  bundled; install it yourself first via Homebrew: `brew install ffmpeg@7`. Try it and report back
  via [Issues](https://github.com/OpportunV/OpportunMediaPlayer/issues), good or bad.
- Ships self-contained (bundles its own .NET 10 runtime) — no separate .NET install needed.
- URL playback is optional and needs [yt-dlp](https://github.com/yt-dlp/yt-dlp) installed
  separately (`winget install yt-dlp.yt-dlp` / `brew install yt-dlp` / `pip install -U yt-dlp`) —
  OMP looks for it on your `PATH` by default, or you can point it at a specific executable from
  Options. Not required for local files.

## Limitations

These are current, code-level limits, not artificial restrictions — worth knowing before you rely
on OMP for a given file:

**Video**
- Only the first video track in a file is used; files with multiple video tracks (e.g. multi-angle)
  can't switch between them.
- Decoding is software-only — no GPU-accelerated decode. Playback of very high-resolution or
  high-bitrate video is limited by CPU decode speed.
- All video is converted to 8-bit BGRA for display regardless of source bit depth — no HDR or
  10-bit-aware rendering.
- Frame buffers are sized once when a file opens; a stream that changes resolution mid-playback
  is not handled.

**Audio**
- Every audio track is downmixed/converted to 16-bit stereo before playback — no surround-sound
  passthrough and no output above 16-bit, regardless of the source format.
- A given output device can only be assigned to one route at a time (you can't send two different
  tracks to the same speaker simultaneously), but the same track can fan out to multiple devices.

**Subtitles**
- Only text-based subtitle formats are supported: SRT, ASS, SSA, WebVTT, and MOV_TEXT. Bitmap-based
  subtitles (PGS, VobSub/DVD, DVB, XSUB) are detected and listed, but can't be routed or rendered.
- Of ASS/SSA styling, only bold, italic, and line breaks are applied. Color, custom fonts,
  position/movement, karaoke, and animation override tags are ignored. Embedded container fonts
  are not used.

**URL playback**
- OMP automatically picks the best format yt-dlp reports for a URL: a combined video+audio
  stream when one exists, otherwise separate video and audio-only streams routed together,
  falling back to audio-only if that's all a page offers. If yt-dlp can't find any playable
  video- or audio-bearing format for the page at all, opening it fails with an error up front.
- A URL's extra audio dubs and caption tracks are only fetched once you actually select them, not
  when the URL first opens — keeps opening fast, but switching to one of them has a short delay
  while it connects.

**General**
- One file (or URL) open at a time — no playlist or queue.
- No chapter markers.
- No DRM/encrypted content support.

## Download

Pre-built binaries are published on the [Releases page](https://github.com/OpportunV/OpportunMediaPlayer/releases):

- **Windows** — `OMP-Setup-<version>.exe` (installer) or `OMP-<version>-win-x64-portable.zip`
  (no install required). Both are unsigned, so SmartScreen will show a "Windows protected your
  PC" warning the first time you run either one — click **More info → Run anyway**.
- **Linux** — `OMP-<version>-x86_64.AppImage` or `OMP-<version>-linux-x64-portable.tar.gz`
- **macOS** — `OMP-<version>-osx-arm64.dmg` (Apple Silicon) or `OMP-<version>-osx-x64.dmg`
  (Intel). Open the `.dmg` and drag `OMP.app` into Applications. It's ad-hoc signed but not
  notarized (no Apple Developer account behind this project), so Gatekeeper will still block it
  once on first launch — right-click `OMP.app` in Applications and choose **Open**, or approve it
  via **System Settings → Privacy & Security** after the first blocked attempt. Also requires
  `brew install ffmpeg@7` beforehand.

Building from source (below) is only needed if you want an unreleased change from `develop` or
there's no release for your platform yet.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/).

```bash
dotnet build OMP.Ui/OMP.Ui.csproj
dotnet run --project OMP.Ui/OMP.Ui.csproj
```

Unit tests (pure logic only, instant, no native dependencies):

```bash
dotnet test OMP.Lib.Tests/OMP.Lib.Tests.csproj
```

UI tests (`OMP.Ui.Tests`) — pure logic plus real headless Avalonia control/window behavior via
`Avalonia.Headless`, no display or native FFmpeg libs needed:

```bash
dotnet test OMP.Ui.Tests/OMP.Ui.Tests.csproj
```

Integration tests (opens real files from `test-fixtures/` through the real engine — needs a
runtime identifier so the matching native FFmpeg libraries get bundled; swap `win-x64` for
`linux-x64` or `osx-arm64`/`osx-x64` as appropriate):

```bash
dotnet test OMP.Lib.IntegrationTests/OMP.Lib.IntegrationTests.csproj -r win-x64
```

## Issues and feedback

Bug reports and feature requests are welcome via [GitHub Issues](https://github.com/OpportunV/OpportunMediaPlayer/issues).

## License

[PolyForm Noncommercial 1.0.0](LICENSE) — free to use, modify, and share for any noncommercial
purpose, with attribution. This is a source-available license, not an OSI-approved open-source
one (no MIT/GPL/Apache-style unrestricted use) — commercial use is not permitted without a
separate agreement.
