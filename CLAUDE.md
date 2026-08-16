# OpportunMediaPlayer

A local media player (Avalonia desktop UI + FFmpeg-based engine) whose key feature is routing
different audio tracks from one file to different audio output devices simultaneously.

- `OMP.Lib` — the playback engine. No UI framework dependency. Stays that way.
- `OMP.Ui` — Avalonia UI, DI composition root, hosts `OMP.Lib` via `IMediaSessionRegistry`.
- `OMP.Lib.Tests` — xUnit. Covers pure/extracted logic only (see Testing below).

`OMP.Ui`'s folder layout is folder-matches-namespace, same convention as `Models`/`Settings`/
`Input`/`Extensions`/`Localization`: `Windows/` (namespace `OMP.Ui.Windows`) holds every dialog
window except `MainWindow` itself (`AboutWindow`, `OptionsWindow`, `HotkeysWindow`, etc. —
`MainWindow` stays at root as the app shell, alongside `App`/`Program`). `Services/` (namespace
`OMP.Ui.Services`) holds non-control helper/service classes (`MainWindowCommands`,
`FullscreenController`, `WindowFactory`, `VideoRenderSurface`, `SubtitleOverlayRenderer`).
`Controls/` is reserved for genuine Avalonia `UserControl`s only (`SpeedFlyoutView`,
`VolumeFlyoutView`) — it used to also hold the `Services/` classes, which is why "is this a real
control or a plain helper" is the test to apply before adding something new to either folder.

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

FFmpeg has its own native logging (`av_log_set_level`/`av_log_set_callback`), separate from
everything above and normally printed straight to stderr, not through `ILogger`/Serilog.
`FFmpegEnvironment.EnsureInitialized` (mirrors `PortAudioEnvironment`'s init-once pattern) sets
it to `AV_LOG_FATAL` once, from `MediaSession`'s constructor — mainly to stop certain files (FLV
audio codecs FFmpeg doesn't implement, malformed `PARAM_CHANGE` side data) from spamming stderr
on every packet. It only touches native FFmpeg output; the app's own Serilog diagnostics are
unaffected.

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

## Video rendering

`MainWindow.Render` copies a decoded frame's pixel data out via `ArrayPool<byte>.Shared`,
synchronously on the render-loop thread, before ever posting to the UI thread. `VideoFrame.DataPtr`
points into `VideoPipeline`'s fixed, round-robin-reused pool of native buffers (`_frameBuffers`) —
unsafe to read once `Render` returns, since decode can cycle back around to the same slot within
milliseconds during post-seek catch-up (decode running at hundreds of fps is normal there). The
dispatch to the UI thread stays a plain, non-blocking `Dispatcher.UIThread.Post` — never `Invoke`.
Blocking the render-loop thread on the UI thread was tried first and made things worse: every
packet channel downstream is `Wait`-mode, demux is a single thread shared with audio, and a
momentarily busy UI thread cascades all the way back through `VideoPipeline`'s frame channel →
`MediaSession`'s video packet channel → the demux thread itself, stalling audio too — confirmed
directly in a real session log showing 16 seconds of frozen video and an empty audio buffer while
the clock kept ticking.

`VideoPipeline.FrameBufferPoolSize` is deliberately double `BufferedFrameCount` (the frame
channel's own capacity), not equal to it: `Enqueue` picks a buffer and `sws_scale`s into it
*before* checking channel capacity, so a same-sized pool and channel let the very next decoded
frame's `sws_scale` call overwrite a still-queued, unrendered frame's data the moment the channel
fills — real corruption, not just a stale frame.

## Seeking

`SeekLookbackSeconds` backs the requested target up by a small margin before calling
`av_seek_frame(AVSEEK_FLAG_BACKWARD)`, but that flag only guarantees landing *at or before* the
point it's given — for content with sparse keyframes, the actual landing point can be seconds (or,
for a large enough keyframe gap, effectively the start of the file) earlier than the lookback
margin alone accounts for. Never assume "landed" means "landed near the target" — downstream code
has to handle a potentially large landing-to-target gap explicitly, not approximate it away.

`ThrottleDemuxAhead` (`MediaSession`) used to mis-pace exactly that gap: `Seek()` rebases the clock
straight to the *requested* target (`_clock.Rebase(targetSeconds)`), and the throttle compared
every demuxed packet's PTS against that already-jumped clock, capping demux to
`MaxDemuxLookaheadSeconds` ahead of it. Every packet between the true landing point and the target
legitimately looked "ahead" of the clock, so the throttle kept re-engaging — turning catch-up
decode (normally near-instant, much faster than real time) into a stall exactly as long as the
landing-to-target gap. Fixed via `_lastSeekTargetSeconds` (the real, ungapped target, set on every
successful seek): a packet whose own PTS is still before it skips throttling entirely, since that
content is bound to be discarded downstream anyway (`AudioPipeline`'s skip-before-target,
`VideoRenderLoop`'s lag-drop) — pacing it to the clock had no benefit and real cost.

`PtsBaselineDetector.DetectOffset` corrects a genuine quirk in standalone mp3/wav/aac containers,
where the first packet's raw decoded PTS after *any* backward seek comes back near 0 regardless of
where the seek actually landed — validated only for audio-only sessions. A video file's streams
can *also* legitimately report a near-0 raw PTS after a seek, for the same sparse-keyframe reason
above — but there it's a true position, not a reporting bug, and the detector's threshold check
(`firstRawSeconds < 1 && anchorSeconds > 1`) can't tell the two cases apart from the numbers alone.
Applying the mp3/wav/aac correction to a video seek is actively harmful, and worse than the
throttle bug above: the wrong offset is computed once and cached for the whole seek generation, so
it never self-corrects. Confirmed directly on a real seek: the "corrected" offset shifted
`AudioPipeline`'s notion of current position permanently ~5s ahead of the audio actually being
decoded (pre-target content played audibly, while the displayed position read the target), and
made the video stream's demux-throttle comparison believe every packet had already reached the
target from the first one — so video's `frameQ` stayed empty indefinitely, not just during a brief
catch-up window, because decode could never gain on a clock the offset made it look like it had
already caught up to. `MediaSession.Seek()` now only computes a real anchor (enabling the
correction) when `_videoPipeline is null`; a video session always passes an anchor of `0`, which
disables the correction outright and trusts raw PTS as ground truth for where a seek landed —
consistent with `ThrottleDemuxAhead`'s own fix above.

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

A `CancellationTokenSource`'s `.Token` getter throws `ObjectDisposedException` once the source
itself has been disposed — `IsCancellationRequested` doesn't. A loop that reads `.Token` fresh
inside a closure dispatched later in the same iteration (e.g. `MediaSession.DemuxLoop`'s
video-packet dispatch) can race `Dispose()` disposing the source out from under it, crashing with
an unhandled exception on a background thread. Capture `.Token` once into a local at the top of
the loop instead — a token obtained before disposal stays perfectly usable afterward, since it's a
lightweight reference to the source's state, not tied to the source object still being alive.

## Type visibility and sealing

Default to `internal`, not `public` — a type is `public` only if something outside its own
assembly actually references it, or if it's a type `Microsoft.Extensions.DependencyInjection`
constructs directly via `services.AddTransient<T>()`/`AddSingleton<T>()` (see gotcha below).
`OMP.Lib`'s public surface is what `OMP.Ui` consumes (`IMediaSession`, `IMediaSessionRegistry`,
`MediaSessionRegistry`, `AudioStream`, `AudioOutput`, `VideoFrame`); everything else there —
`MediaSession` itself, `AudioPipeline`, `VideoPipeline`, scanners, etc. — is `internal`.
`OMP.Ui` has no external consumers at all, so the only things `public` there are `App`,
`MainWindow`, all six `Windows/` dialog windows (`OptionsWindow`, `AboutWindow`,
`AudioOutputWarningWindow`, `HotkeysWindow`, `OpenFileErrorWindow`, `SubtitleZoneEditorWindow` —
Avalonia XAML code-behind convention) and the interfaces/context type that cross into
`MainWindow`'s public constructor signature (`IMainWindowCommands`, `IMainWindowHotkeyService`,
`IWindowFactory`, `MainWindowCommandContext`) — their concrete implementations
(`MainWindowCommands`, `MainWindowHotkeyService`, `WindowFactory`) stay `internal`, since an
internal class implementing a public interface is completely fine, and nothing outside the
assembly ever names the concrete type. Default to `sealed` unless a class is deliberately
designed as a base type — nothing in this codebase currently is.

Gotcha: `Microsoft.Extensions.DependencyInjection`'s default `ServiceProvider` only activates a
type via `services.AddTransient<T>()`/`AddSingleton<T>()` (registering the concrete type
directly, as `MainWindow` and every `Windows/` dialog are) if it has a **public** constructor —
it will not use an internal one, even same-assembly, unlike general reflection which doesn't
care. We tried keeping those constructors internal with an explicit factory registration
(`services.AddTransient(sp => new MainWindow(...))`) to avoid this, but decided the simpler,
more conventional `AddTransient<MainWindow>()` was worth the small public surface it costs —
hence those constructors, and the handful of types feeding their signatures, staying public.
`Microsoft.Extensions.Options`' binding (`IOptions<T>`/`services.Configure<T>`) does *not* have
this restriction — internal options types work fine there, it's specifically `ServiceProvider`'s
constructor-activation path that's public-only.

## UI theming, icons, and button consistency

Custom theme-variant-aware brushes (bar/flyout backgrounds and foregrounds — anything that needs
to look different in Light vs Dark and isn't just relying on FluentTheme's own defaults) live in
`App.axaml`'s `Application.Resources`, via `ResourceDictionary.ThemeDictionaries` with `Light`/
`Dark` keys. That block needs an explicit `<ResourceDictionary>` wrapper around the whole
`Application.Resources` content — the bare `<Application.Resources><Color .../></Application
.Resources>` shorthand (used for the `SystemAccentColor*` overrides) does not support
`ResourceDictionary.ThemeDictionaries` as a sibling; without the wrapper, Avalonia's XAML compiler
fails with `AVLN2200`/`AXN0002`-style errors depending on exactly how it's misused. Confirmed by
testing, not guessed — Avalonia's Fluent internals are compiled into the NuGet package, not loose
XAML source, so nothing here should be assumed to work from WPF muscle memory without checking.

Every icon in the app is a hand-drawn vector `StreamGeometry` (or `GeometryGroup`, for icons that
mix filled shapes like dots with stroked lines) in `OMP.Ui/Assets/Icons.axaml`, merged into
`App.axaml` via `ResourceDictionary.MergedDictionaries` and consumed via `{StaticResource
SomeIconGeometry}` on a `Path` inside a `Viewbox`/`Canvas`. Deliberately not emoji or text glyphs
(🔇, ✕, +, -) — those were tried first for the compact inline-row buttons and are a dead end:
they render in their own fixed color regardless of the button's `Foreground`, so they can't track
the app's theme at all, and their centering/baseline varies by platform emoji font. Icon `Path`s
bind `Fill`/`Stroke` to `{Binding $parent[Button].Foreground}` (or `$parent[ToggleButton]`) rather
than to a specific brush resource — that way an icon automatically matches whatever foreground
color is correct for its context (`OverlayBarForegroundBrush` on the main bar, `FlyoutForegroundBrush`
inside a themed flyout, FluentTheme's own default elsewhere) without the icon needing to know which
context it's in, and without guessing FluentTheme's own internal foreground resource key name.

Two style classes in `App.axaml` (`Button.icon-button, ToggleButton.icon-button` at 36×36 for the
main playback bar; the `-sm` variant at 28×28 for compact inline-row actions in Options/flyouts)
set `Width`/`Height`/`Padding="0"`/centered content alignment once, instead of repeating those four
attributes on every icon button by hand — that repetition is exactly how the main bar and the
compact rows drifted out of sync with each other before (`32×28`, `28×26`, `Width` with no `Height`
all showed up across different files). Text-labeled buttons (`Cancel`/`Save`/`Close`/`Edit`/
`Reset`/`GitHub`/`Add Zone`, and `SpeedButton`) are deliberately excluded from both classes — they
need to size to their text, not be forced square.

`Flyout` has no `Background`/`Foreground` property of its own — to theme a `Flyout`'s popup chrome,
tag it via `flyout.FlyoutPresenterClasses.Add("app-flyout")` in code and target
`Style Selector="FlyoutPresenter.app-flyout"` in `Application.Styles`. Both `SpeedFlyoutView`'s and
`VolumeFlyoutView`'s flyouts share that one class/style rather than each getting their own, since
there's currently no reason for them to look different.

`Grid.IsSharedSizeScope="True"` + matching `ColumnDefinition.SharedSizeGroup` names is how
`OptionsWindow`'s Audio tab keeps its header row's columns aligned with each data row's columns
(two separate `Grid` instances — the header and each `ItemsControl` row template — which Avalonia
sizes independently by default, so column boundaries drift the moment their `Auto`-column content
differs, e.g. the header has nothing in the Mute column but a row does). Confirmed by testing:
`SharedSizeGroup` does not play well with `*`-sized columns split across separate `Grid`s (a `*`
column's width is resolved from that one `Grid`'s own available space, not shared across grids the
way `SharedSizeGroup` shares `Auto` columns), so the Output/Stream columns had to switch from
`*,*` to `Auto,Auto` with an explicit `MaxWidth` cap on their content to get real cross-grid
alignment.

`ProgressSlider` proves FluentTheme's `Slider` can be re-skinned per-instance, without a full
custom `ControlTemplate`, by overriding the `DynamicResource` keys its own template already reads
from — `SliderTrackThemeHeight` (track thickness), `SliderHorizontalThumbWidth`/
`SliderHorizontalThumbHeight`/`SliderThumbCornerRadius` (thumb size/shape), `SliderHorizontalHeight`
(the template's own `MinHeight`, i.e. the overall hit-test envelope) — declared directly in a
`<Slider.Resources>` block on that one `Slider`, leaving every other `Slider` in the app on
FluentTheme's defaults. These key names came from reading the actual Avalonia 11.3.11 source, not
guessed — get this wrong and you silently re-skin nothing, or every `Slider` in the app instead of
one. One thing this trick can't reach: the `RepeatButton` track segments' own ~10px hit-padding is
hardcoded inside a *nested* `ControlTemplate` that Fluent's outer `Slider` template pulls in via
`StaticResource` (resolved once, at the theme's own parse time) rather than `DynamicResource` — a
per-instance resource override never reaches it, so shrinking that padding would need a full
custom `Slider` template, not a resource override.

`OptionsWindow`'s Audio tab enforces that an *output* can only carry one active route at a time
(`UpdateOutputSelector` excludes already-used outputs — you can't send two different tracks to one
physical speaker), but deliberately does **not** apply the same exclusivity to *streams*: the same
audio track can legitimately be routed to more than one output at once (e.g. the main audio track
sent to both speakers and a headset simultaneously) — matching the readme's headline feature. Both
the per-row `AudioRouteRow.AvailableStreamOptions` and the bottom-of-tab `StreamSelector` are left
unfiltered by design; only `OutputSelector`/`UpdateOutputSelector` filters.

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

**macOS gets no bundled FFmpeg at all — it resolves against a system install instead.** Unlike
Windows/Linux, no trusted source publishes redistributable per-library FFmpeg `.dylib`s for
macOS: BtbN/FFmpeg-Builds (the source for the Windows/Linux libs) has never covered macOS, and
the usual macOS FFmpeg download sites (evermeet.cx, Martin Riedl's build server) only ship a
single static `ffmpeg`/`ffprobe` executable, not the separate libs `FFmpeg.AutoGen` P/Invokes
against — and evermeet.cx is Intel-only besides. So `OMP.Lib/Libs/osx-x64`/`osx-arm64` don't
exist, and `OMP.Ui.csproj` has no macOS `Content` block. Instead, `OMP.Ui/Services
/FFmpegLibraryLocator.CreateOptions` (macOS-only probing internally, called once from `Program.cs`
as `services.AddSingleton(FFmpegLibraryLocator.CreateOptions())`) checks Homebrew's keg-only
`ffmpeg@7` formula install paths (`/opt/homebrew/opt/ffmpeg@7/lib` on Apple Silicon,
`/usr/local/opt/ffmpeg@7/lib` on Intel) for `libavcodec.61.dylib` and returns a populated
`OMP.Lib.NativeLibraryOptions` — a plain POCO living in `OMP.Lib` (mirrors `PlaybackTuningOptions`:
the type itself is host-agnostic data, only its *value* is host-specific). Registering the whole
options instance, rather than a bare `string?`, keeps `Program.cs`'s `ConfigureServices` a flat list
of one-line registrations with no branching of its own, and lets `MediaSessionRegistry`/
`MediaSession` keep taking it as an ordinary constructor-injected dependency instead of needing a
factory-delegate registration. `MediaSession` reads `.FFmpegLibraryDirectory` and passes it to
`FFmpegEnvironment.EnsureInitialized`, which sets `ffmpeg.RootPath` before any other FFmpeg call —
consistent with "`OMP.Lib` stays host-agnostic" above (the search logic lives in `OMP.Ui`; `OMP.Lib`
only receives the resolved directory). It has to be `ffmpeg@7` specifically, not the default
`ffmpeg` formula: Homebrew's plain `ffmpeg` had moved to major version 9 by the time this was
written, which doesn't match `FFmpeg.AutoGen 7.1.1`'s expected sonames (`avcodec-61` etc.) — an
ABI mismatch that a newer major version won't reliably paper over. If no matching directory is
found, `ffmpeg.RootPath` is left unset and the first native FFmpeg call throws
`DllNotFoundException` when a file is opened; `MainWindow.OpenPath`'s existing catch-all already
surfaces that through `OpenFileErrorWindow`, and on macOS specifically swaps in
`OpenFileError_FFmpegMacHeading` (pointing at `brew install ffmpeg@7`) instead of the generic
corrupted-file heading. None of this has been runtime-verified on real macOS hardware — no Mac
was available when it was written, same caveat as the rest of the macOS work in this project.

## Testing

`OMP.Lib.Tests` covers logic that's genuinely unit-testable without FFmpeg or a real audio
device: `PlaybackClock`, `AudioSpeedProcessor`, `AudioGainProcessor`, `AudioDelayProcessor`,
`PtsBaselineDetector`, `PipelineWorker`, `ChannelExt`, `EndOfStreamTracker`, subtitle cue-store
and text-parsing logic. `MediaSession` / `MediaSessionRegistry` need a real file and real FFmpeg
native libs to construct, so they're not unit tested — verify those manually. Avalonia-side
classes (`FullscreenController`,
`VideoRenderSurface`, `WindowFactory`) are written with plain constructor dependencies so
Avalonia-headless tests can be added later, but that test host isn't wired up yet.
