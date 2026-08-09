# Third-Party Notices

Opportun Media Player is distributed under the PolyForm Noncommercial License 1.0.0 (see
`LICENSE`). It bundles or depends on the following third-party software, each under its own
license as noted below.

## FFmpeg

- **Used for:** media demuxing, decoding, and resampling (`OMP.Lib/Libs/win/`,
  `OMP.Lib/Libs/linux-x64/`, via `FFmpeg.AutoGen`)
- **License:** GNU Lesser General Public License, version 3 or later (LGPL-3.0-or-later)
- **Source:** <https://ffmpeg.org/>

The bundled FFmpeg shared libraries (`avcodec`, `avformat`, `avutil`, `swresample`, `swscale`)
are built without `--enable-gpl`/`--enable-nonfree`, so no GPL- or nonfree-licensed FFmpeg
component (e.g. libx264, libx265) is included. LGPL requires that users be able to relink
against a modified version of these libraries; you may replace the bundled files in
`OMP.Lib/Libs/` with a compatible build of your own. FFmpeg's full LGPL 3 license text is
available at <https://www.gnu.org/licenses/lgpl-3.0.html>.

## FFmpeg.AutoGen

- **Used for:** C# P/Invoke bindings to FFmpeg (`OMP.Lib`)
- **License:** MIT
- **Source:** <https://github.com/Ruslan-B/FFmpeg.AutoGen>

## PortAudioSharp2

- **Used for:** cross-platform audio output (`OMP.Lib/Audio/Output/`)
- **License:** Apache License 2.0
- **Source:** <https://github.com/csukuangfj/PortAudioSharp2>

PortAudioSharp2 bundles pre-compiled PortAudio, itself distributed under the MIT-style
PortAudio license (<http://www.portaudio.com/license.html>).

## NAudio.Core

- **Used for:** wave provider abstractions (`IWaveProvider`, `BufferedWaveProvider`) used in the
  audio pipeline
- **License:** MIT
- **Source:** <https://github.com/naudio/NAudio>

## Avalonia

- **Used for:** UI framework (`OMP.Ui`) — includes `Avalonia`, `Avalonia.Desktop`,
  `Avalonia.Themes.Fluent`, `Avalonia.Controls.ColorPicker`
- **License:** MIT
- **Source:** <https://github.com/AvaloniaUI/Avalonia>

### Avalonia.Fonts.Inter

- **Used for:** bundled Inter typeface
- **License:** packaging code is MIT; the Inter font itself is licensed under the SIL Open Font
  License 1.1
- **Source:** <https://github.com/AvaloniaUI/Avalonia>, <https://rsms.me/inter/>

## Microsoft.Extensions.* (Logging.Abstractions, DependencyInjection, Hosting)

- **Used for:** logging abstraction (`OMP.Lib`), DI container and generic host (`OMP.Ui`)
- **License:** MIT
- **Source:** <https://github.com/dotnet/runtime>, <https://github.com/dotnet/extensions>

## Serilog

- **Used for:** structured logging implementation and sinks (`OMP.Ui`) — includes `Serilog`,
  `Serilog.Extensions.Hosting`, `Serilog.Settings.Configuration`, `Serilog.Sinks.Console`,
  `Serilog.Sinks.File`, `Serilog.Enrichers.Thread`
- **License:** Apache License 2.0
- **Source:** <https://github.com/serilog/serilog>

## Tmds.DBus.Protocol

- **Used for:** D-Bus interaction on Linux (transitive dependency via Avalonia)
- **License:** MIT
- **Source:** <https://github.com/tmds/Tmds.DBus>
