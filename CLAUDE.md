# OpportunMediaPlayer

A local media player (Avalonia desktop UI + FFmpeg-based engine) whose key feature is routing
different audio tracks from one file to different audio output devices simultaneously.

- `OMP.Lib` — the playback engine. No UI framework dependency. Stays that way.
- `OMP.Ui` — Avalonia UI, DI composition root, hosts `OMP.Lib` via `IMediaSessionRegistry`.
- `OMP.Lib.Tests` — xUnit. Covers pure/extracted logic only (see Testing below).

## Member ordering

Within a class, group members by descending accessibility (public → internal → protected →
private). Within each accessibility group: properties first, then fields, with constants last
within that group's fields (not at the bottom of the whole class — just the bottom of their own
accessibility group's fields). Constructor after fields. Methods last, also grouped by
descending accessibility.

`stylecop.json` + `.editorconfig` enforce the accessibility-grouping part (SA1202) at build
time. They do **not** enforce properties-before-fields or constants-last — StyleCop's SA1201/
SA1203/SA1214 have a hardcoded fields-before-properties, constants-first kind order that can't
be reconfigured to match this project's convention (confirmed by testing: those rules flagged
our own correctly-ordered files), so they're deliberately switched off. Apply that part by hand
— when writing or reviewing a class, that's the part to double check.

## OMP.Lib stays host-agnostic

`OMP.Lib` must not know about config files, hosting, or dev/debug conveniences — those are
`OMP.Ui` concerns. If a feature needs a knob (e.g. a debug-only behavior toggle), define the
options type and the config binding in `OMP.Ui`, and have `OMP.Ui` push the *result* into
`OMP.Lib` (constructor args, method calls, event subscriptions) rather than having `OMP.Lib`
read configuration itself.

Constants that are genuinely engine tuning (buffering, sync tolerances, algorithm parameters —
see `MediaSession`, `AudioPipeline`, `AudioSpeedProcessor`) are a different case: they can live
in `OMP.Lib` as `const` fields even though they're also arguably "config", because moving them
requires understanding playback-loop or resampling internals to change safely. Don't confuse
"engine tuning constant" with "host/debug configuration" — the former stays as a hardcoded
constant unless a specific need to tune it externally comes up; the latter is always bound from
`OMP.Ui`'s config and pushed in.

`Microsoft.Extensions.Logging.Abstractions` is a deliberate, bounded exception to "no
`Microsoft.Extensions.*` in `OMP.Lib`". It contains only `ILogger`/`ILoggerFactory`/`LogLevel` —
no config, no container, no hosting — so it carries none of the coupling the rule exists to
prevent. The logging *implementation* and all of its configuration stay in `OMP.Ui`. Don't read
this as licence to add the other `Microsoft.Extensions.*` packages.

## Logging

`OMP.Lib` only ever sees `ILogger`. Everything else — sinks, levels, paths, retention — lives in
the `Serilog` section of `OMP.Ui/appsettings.json`; `Program.cs` only calls
`ReadFrom.Configuration`. Serilog expands `%ENVVAR%` in config values, which is what lets the file
path (`%OMP_LOG_DIR%/OpportunMediaPlayer/logs/omp-.log`) stay in config rather than being built in
code — `%OMP_LOG_DIR%` itself is set by `Program.cs` from `Environment.SpecialFolder
.LocalApplicationData` (a genuinely cross-platform BCL API) before Serilog reads config, rather
than hardcoding the Windows-only `%LOCALAPPDATA%` env var directly in the config string, which
silently wrote to the wrong place on Linux (unresolved `%VAR%` tokens are left literal by
`Environment.ExpandEnvironmentVariables`, not replaced with empty string). Logs are capped at
10 MB × 7 files; `rollOnFileSizeLimit` is what makes the size limit roll rather than silently stop
logging, so don't drop it.

`ILoggerFactory`, not `ILogger<T>`, is threaded through the engine: DI gives it to
`MediaSessionRegistry`, which hands it to `MediaSession`, which hands it to every pipeline and
scanner, each doing `loggerFactory.CreateLogger<TSelf>()`. `ILogger<AudioPipeline>` is not an
option — `AudioPipeline` is `internal` and `MediaSessionRegistry` is `public`, so it cannot
appear in a public constructor signature. The factory also mints a logger per instance (the
number of pipelines is variable) and preserves per-type category names, which is what makes
`MinimumLevel:Override` per-subsystem filtering work. There is deliberately no
`NullLoggerFactory` default — optional logging is how an engine goes silent again; tests pass
`NullLoggerFactory.Instance` explicitly.

| Level | Use | Examples |
|---|---|---|
| `Error` | Operation failed | Codec open failure, presentation-loop exception, endpoint enumeration failure |
| `Warning` | Degraded but recovered | Seek failed, zero outputs, first decode-return-code failure per pipeline |
| `Information` | User-visible lifecycle | App start/stop, file opened, routes changed |
| `Debug` | Construction/teardown, scan results | Per-stream scan detail, per-route pipeline build |
| `Trace` | Per-packet anomalies | `PumpToOutput` buffer rejection, missing metadata tags. Off in all shipped configs |

**Nothing may be logged per frame, per packet, or per chunk.** Not in `VideoRenderLoop`'s
iteration body (except its rate-limited error handler), not in either pipeline's
`avcodec_receive_*` loop, not per demuxed packet. At 60 fps with several audio routes that is
>10,000 calls/second. Failures that are inherently per-packet (`avcodec_send_packet`,
`swr_convert`) log the first occurrence and then count, reporting the total on `Dispose` —
`AudioPipeline` is the reference implementation. `MediaSession.LogLoopError` is the reference for
collapsing a repeating exception down to one line per interval.

**Use message templates, never interpolated strings.** Measured on this exact call shape with the
level disabled: interpolated 112 B/call, template 88 B/call, template behind an `IsEnabled` guard
0 B/call. The template still allocates because the arguments box into an `object?[]` at the call
site, before `ILogger` gets a chance to check the level.

**Do not blanket-apply Rider's "evaluation of this argument may be expensive" `IsEnabled`
suggestion.** That 88 B is only worth an `if` when the call site is hot, and almost none here are
— every logging call in `OMP.Lib` is once per session, once per pipeline, or already behind an
`Interlocked.Increment(...) == 1`. Adding the guard everywhere buys nothing and costs four lines
of noise per site. There is currently exactly one call site that earns it: the `LogTrace` in
`AudioPipeline.PumpToOutput`, which sits on a per-chunk path. Guard a site when it is genuinely
hot; otherwise leave it. If per-packet diagnostics are ever genuinely needed, reach for
`[LoggerMessage]` source-generated partial methods (also measured at 0 B/call) rather than
hand-written guards — but note the class has to become `partial`.

## Audio output and volume

`AudioOutput.Id` is PortAudio's own global device index (`OutputScanner`, in
`OMP.Lib/Audio/Output/`) — the same index both enumerates a device and opens a stream on it via
`PortAudioOutput`, so unlike the pre-PortAudio NAudio implementation there's no separate
enumeration-vs-open index space to reconcile (that mismatch, and the `WaveOutDeviceResolver`
name-matching hack it needed, no longer exist). On Windows, PortAudio otherwise lists every
physical device once per host API (MME, DirectSound, WASAPI, WDM-KS); `OutputScanner` filters to
the WASAPI host API's devices only — `OMP.Lib/Interop/PortAudioHostApi.cs` adds supplemental
P/Invoke for `Pa_GetHostApiInfo`, which PortAudioSharp2 doesn't bind itself — to avoid showing
3-4 duplicate entries per physical device. Linux/macOS don't have this duplication (ALSA and
CoreAudio each expose a single host API), so the filter is a no-op there. WASAPI shared-mode
streams also reject a sample rate that doesn't match the device's own mix rate (confirmed via
`PaErrorCode.InvalidSampleRate`), so `AudioPipeline` resamples to `IAudioOutput.PreferredSampleRate`
(queried from the target device) rather than a fixed constant.

PortAudioSharp2's binding is callback-based only (no blocking `Pa_WriteStream`) — `PortAudioOutput`
pulls PCM from the routed `IWaveProvider` inside PortAudio's own native audio-thread callback,
rather than from a dedicated managed thread the way `WaveOutEvent` used to.

Gain is applied in `GainWaveProvider.Read`, between the `BufferedWaveProvider` and `IAudioOutput`,
so a volume change is audible within the driver buffer (~150 ms). Applying it earlier — in the PCM
path or at `PumpToOutput` — puts it behind `BufferDurationSeconds` of decoded audio and bakes the
gain into buffered data. `AudioGainProcessor` holds the arithmetic so it stays unit-testable,
mirroring `AudioSpeedProcessor`.

Volume range is `[AudioVolumeLimits.Min, AudioVolumeLimits.Max]` = `[0, 2.0]` (0–200%), not `[0, 1]`
— sliders go past 100% on purpose, matching the "boost" ceiling other players (e.g. VLC) offer.
`AudioGainProcessor.ToAmplitude` uses a square-law taper up to unity (perceptual) and switches to
linear above it (a deliberate boost has no perceptual curve to approximate — 200% volume is exactly
2x amplitude, +6dB). Clipping above unity is expected and safe: `Apply`'s `short.MinValue`/`MaxValue`
clamp is what keeps a boosted full-scale sample from wrapping sign instead of just distorting.

Master and per-output volume are composed as a *product of tapers*, so each slider's feel stays
independent of the other's position — note two boosted sliders multiply (150% × 150% = 225%
effective), there's no combined ceiling beyond each individual `Max`. Volume lives on
`MediaSession`, keyed by `AudioOutput.Id`, and is re-pushed after `SetAudioRoutes` rebuilds the
pipelines — the same way speed is. Per-output volume deliberately does **not** live on
`AudioRoute`: `SetAudioRoutes` disposes and recreates every `IAudioOutput`, so routing a slider
through it would tear down devices on every tick.

## Playback speed

`MediaSession.SetSpeed` does a **narrow flush**, deliberately less than `Seek`: no seek-generation
bump, no packet-channel drain, no `av_seek_frame`, no video flush, no session Pause/Play. The
demuxer is already reading from the right place — only the already-decoded PCM sitting in each
`AudioPipeline` needs discarding and re-decoding at the new rate, or it would keep playing at the
old speed for up to `BufferDurationSeconds`. That flush loop runs under `_seekSync` (not a new
lock): `AudioPipeline.Flush()` drains the pipeline's single-reader `_decodedPcmChannel`, and
`PumpToOutput` on the presentation thread reads that same channel every iteration. `SetSpeed` is
called from the UI thread, so without serializing against a concurrent `Seek()` the two could touch
the channel's reader side from two threads at once — the channel opts into single-reader fast paths
and isn't safe for that. This mirrors (and doesn't widen) a race that already exists around `Seek`.

`PlaybackSpeedPresets` (the YouTube-style preset list) and `PlaybackSpeedLimits` both live in
`OMP.Lib`, not `OMP.Ui`: they're the engine's own declared set of supported rates, not host
configuration, and `OMP.Lib.Tests` is the only test project that exists. `Next`/`Previous` saturate
at the ends rather than wrapping — jumping from 2× to 0.5× on a keypress would be user-hostile.

The UI never calls `SetSpeed` per pixel of a slider drag — `SpeedFlyoutView` mirrors `ProgressSlider`'s
drag pattern (commit `92fcfc5`): update the readout only while dragging, commit once on release.
Every commit reads the applied value back from `session.Speed` rather than trusting the requested
one, so the displayed speed reflects the engine's own clamp. `MediaSessionRegistry.Open` forces
`SetSpeed(1)` unconditionally on every open, so restoring a persisted speed has to happen from
`MainWindow.OnSessionChanged` (after that reset), not from the registry.

Persisted volume is keyed by `FriendlyName`, never by `AudioOutput.Id` — the Id shifts as soon as a
device is plugged in or removed.

## Threading

Playback worker threads (demux/audio/video/render) are identified by `PipelineWorkerRole`
(`OMP.Lib/Threading/PipelineWorkerRole.cs`), not raw strings — gives compile-time-checked
identity and doubles as a tag for future structured logging. Don't reach for empty
subclass-per-role types to get "stronger" identity than an enum already provides; that's
inheritance used for labeling, not behavior, and is the pattern to avoid here.

`PlaybackClock` locks its state with a plain `Lock`/short critical section (field reads/writes,
no I/O, no blocking, no nested locks) — that's the correct, unremarkable use of a lock, not a
smell. Only worry about locking when critical sections do real work, block, or nest.

`ChannelExt` (`OMP.Lib/Extensions/ChannelExt.cs`) gives `ChannelWriter<T>`/`ChannelReader<T>` a
genuinely synchronous, thread-blocking `TryWriteBlocking`/`TryReadBlocking` (bool-returning, out
param for read) — `System.Threading.Channels` is async-only and has no native blocking API, and
this codebase's loops are OS threads, not async/await, so something has to bridge that. It's
sync-over-async (`.AsTask().Wait(token)`), which isn't free (forces a `Task` even on channel
operations that would otherwise complete synchronously) — `System.Collections.Concurrent
.BlockingCollection<T>` would avoid that allocation with a genuinely blocking implementation, but
doesn't support `BoundedChannelFullMode.DropOldest` (used by `MediaSession`'s audio packet
channel) without reimplementing that eviction by hand, so it's not a drop-in swap. Worth
revisiting only if profiling ever shows this allocation matters — unlikely, given the ffmpeg
decode work around every call site dominates. The `bool`/`out` shape (rather than swallowing
`OperationCanceledException` and returning `default!`) is deliberate: a silently-returned
default value is indistinguishable from a legitimately-default item, which is exactly the kind
of bug this shape avoids — see `PipelineWorker.TryWaitIfPaused()` for the same pattern.

## Type visibility and sealing

Default to `internal`, not `public` — a type is `public` only if something outside its own
assembly actually references it, or if it's a type `Microsoft.Extensions.DependencyInjection`
constructs directly via `services.AddTransient<T>()`/`AddSingleton<T>()` (see gotcha below).
`OMP.Lib`'s public surface is what `OMP.Ui` consumes (`IMediaSession`, `IMediaSessionRegistry`,
`MediaSessionRegistry`, `AudioStream`, `AudioOutput`, `VideoFrame`); everything else there —
`MediaSession` itself, `AudioPipeline`, `VideoPipeline`, scanners, etc. — is `internal`.
`OMP.Ui` has no external consumers at all, so the only things `public` there are `App`,
`MainWindow`, `OptionsWindow` (Avalonia XAML code-behind convention) and the interfaces/context
type that cross into `MainWindow`'s public constructor signature (`IMainWindowCommands`,
`IMainWindowHotkeyService`, `IWindowFactory`, `MainWindowCommandContext`) — their concrete
implementations (`MainWindowCommands`, `MainWindowHotkeyService`, `WindowFactory`) stay
`internal`, since an internal class implementing a public interface is completely fine, and
nothing outside the assembly ever names the concrete type. Default to `sealed` unless a class
is deliberately designed as a base type — nothing in this codebase currently is.

Gotcha: `Microsoft.Extensions.DependencyInjection`'s default `ServiceProvider` only activates a
type via `services.AddTransient<T>()`/`AddSingleton<T>()` (registering the concrete type
directly, as `MainWindow` and `OptionsWindow` are) if it has a **public** constructor — it will
not use an internal one, even same-assembly, unlike general reflection which doesn't care. We
tried keeping those two constructors internal with an explicit factory registration
(`services.AddTransient(sp => new MainWindow(...))`) to avoid this, but decided the simpler,
more conventional `AddTransient<MainWindow>()` was worth the small public surface it costs —
hence those two constructors, and the handful of types feeding their signatures, staying public.
`Microsoft.Extensions.Options`' binding (`IOptions<T>`/`services.Configure<T>`) does *not* have
this restriction — internal options types work fine there, it's specifically `ServiceProvider`'s
constructor-activation path that's public-only.

## Resource lifetime

Classes that own something live (a `DispatcherTimer`, a subscription to another object's event,
a native-backed resource like `WriteableBitmap`) implement `IDisposable` and get disposed
explicitly by whoever owns their lifetime (e.g. `MainWindow.OnClosed` disposes
`FullscreenController` and `VideoRenderSurface`). Don't rely on GC/finalizers for these.

## Native library bundling

FFmpeg's (and, going forward, any other engine dependency's) native libs live under
`OMP.Lib/Libs/<rid>/` — co-located with the P/Invoke code in `OMP.Lib` that consumes them — but
the RID-conditional `Content`/`CopyToOutputDirectory` items that actually bundle them into a
build live in `OMP.Ui.csproj`, not `OMP.Lib.csproj`, reaching into `OMP.Lib`'s `Libs` folder by
relative path. This looks like it violates the file's own home, but it's forced: confirmed by
testing that `$(RuntimeIdentifier)` does not reliably flow into a referenced library project's
own item evaluation via `ProjectReference` — even an explicit `dotnet build OMP.Ui -r win-x64`
copied nothing when the Content block lived in `OMP.Lib.csproj`. Only the project actually being
published is guaranteed to have `$(RuntimeIdentifier)` resolved, so RID-conditional bundling has
to be declared there.

This also matters for cross-publishing — e.g. `dotnet publish -r linux-x64` run from a Windows
host, as when testing via a VirtualBox shared folder. An `IsOSPlatform()`-based condition checks
the build host's OS, not the target RID, and silently skips the target platform's libs in that
scenario; `$(RuntimeIdentifier.StartsWith(...))` is the correct check, and it only works reliably
in `OMP.Ui.csproj` for the reason above.

`OMP.Ui.csproj` also pins `<PublishSingleFile>false</PublishSingleFile>` — `FFmpeg.AutoGen`'s
`DynamicallyLoadedBindings` resolves native functions via `Marshal.GetDelegateForFunctionPointer`,
which throws `NotSupportedException` when the app is bundled into a single file. A publish
profile that passes `-p:PublishSingleFile=true` overrides this, so it isn't foolproof — if a
Linux/macOS publish throws `NotSupportedException` out of `DynamicallyLoadedBindings.Initialize`,
check for that first.

## Testing

`OMP.Lib.Tests` covers logic that's genuinely unit-testable without FFmpeg or a real audio
device: `PlaybackClock`, `AudioSpeedProcessor`, `PipelineWorker`. `MediaSession` /
`MediaSessionRegistry` need a real file and real FFmpeg native libs to construct, so they're not
unit tested — verify those manually. Avalonia-side classes (`FullscreenController`,
`VideoRenderSurface`, `WindowFactory`) are written with plain constructor dependencies so
Avalonia-headless tests can be added later, but that test host isn't wired up yet.
