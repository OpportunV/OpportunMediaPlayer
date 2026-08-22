# OpportunMediaPlayer

A local media player (Avalonia desktop UI + FFmpeg-based engine) whose key feature is routing
different audio tracks from one file to different audio output devices simultaneously.

- `OMP.Lib` — the playback engine. No UI framework dependency. Stays that way.
- `OMP.Ui` — Avalonia UI, DI composition root, hosts `OMP.Lib` via `IMediaSessionRegistry`.
- `OMP.Lib.Tests` — xUnit. Covers pure/extracted logic only (see Testing below).
- `OMP.Ui.Tests` — xUnit + `Avalonia.Headless`. Covers pure/extracted UI logic plus real headless
  control/window behavior (see UI Testing below).

`OMP.Ui`'s folder layout is folder-matches-namespace, same convention as `Models`/`Settings`/
`Input`/`Extensions`/`Helpers`/`Localization`: `Windows/` (namespace `OMP.Ui.Windows`) holds every
dialog window except `MainWindow` itself (`AboutWindow`, `OptionsWindow`, `HotkeysWindow`, etc. —
`MainWindow` stays at root as the app shell, alongside `App`/`Program`). `Services/` (namespace
`OMP.Ui.Services`) holds non-control classes with real identity/state/lifecycle
(`MainWindowCommands`, `FullscreenController`, `WindowFactory`, `VideoRenderSurface`,
`SubtitleOverlayRenderer`) — constructed instances a window owns and disposes, not stateless math.
`Controls/` is reserved for genuine Avalonia `UserControl`s only (`SpeedFlyoutView`,
`VolumeFlyoutView`) — it used to also hold the `Services/` classes, which is why "is this a real
control or a plain helper" is the test to apply before adding something new to either folder.

`Extensions/` (namespace `OMP.Ui.Extensions`) vs `Helpers/` (namespace `OMP.Ui.Helpers`) is a
second, narrower distinction, settled while building out `OMP.Ui.Tests`: a class belongs in
`Extensions/`, written with C# 13's `extension(...)` block syntax, only when its receiver type is
already domain-specific (`AudioStream`, `AudioOutput`, `SubtitleZone`, `TimeSpan`, ...) *and* the
operation only needs the receiver's own data — `TimeFormat.Format()` on any `TimeSpan`,
`OutputVolumeExt`'s methods on `AudioOutput`/`AudioRoute` lists, `UserSettingsServiceExt` on
`IUserSettingsService`. A method whose receiver is a bare primitive/generic (`double`, `string`,
`T`) or that needs a whole second, unrelated parameter to mean anything goes in `Helpers/` instead,
as a plain static method — extending `double` with a speed-specific `.Format()`
(`PlaybackSpeedFormat`) or `string` with `.IsSupportedMediaFile(...)` (`MediaFileType`) would let
any unrelated call site compile while silently doing the wrong thing, since nothing about a bare
`double`/`string` says "this specifically means speed" or "this specifically means a media path".
`OptionsSelector.AvailableOptions<T, TKey>` is `Helpers/` for the same reason — extending every
`IEnumerable<T>` in the codebase with an "available options" concept that only makes sense for one
specific screen. `VideoLetterbox` and `SubtitleZoneGeometry` are `Helpers/` too: pure, stateless
coordinate math has no identity or lifecycle, so `Services/` was the wrong home even though nothing
technically stopped them living there. Not enforced by an analyzer (a StyleCop/Roslyn rule for one
project-specific naming convention isn't worth building) — apply this by hand, same as the
member-ordering convention above.

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
3-4 duplicate entries per physical device. macOS's CoreAudio exposes a single host API with no
such duplication, so the filter is a no-op there. Linux's ALSA is likewise a single PortAudio host
API — but, confirmed via real-hardware testing on Astra Linux (~20 listed entries for 2 physical
outputs), that one host API still enumerates every auto-generated virtual/plugin pseudo-device
from `alsa.conf`/`asound.conf` (`front`, `surround40/51/71`, `iec958`, `dmix`, `dsnoop`, `default`,
`pulse`, `sysdefault`, samplerate-converter plugins, ...) alongside the genuine physical hardware
sub-devices, so the WASAPI-style host-API filter alone doesn't help there. `AlsaOutputDeviceFilter`
(`OMP.Lib/Audio/Output/AlsaOutputDeviceFilter.cs`) instead filters Linux device names down to ones
containing the literal `(hw:<card>,<device>)` substring PortAudio's ALSA backend uses for genuine
hardware sub-devices (e.g. `"HD-Audio Generic: ALC3234 Analog (hw:0,0)"`), dropping alias/plugin
names that lack it (`"front:CARD=PCH,DEV=0"`, `"pulse"`, ...) — collapsing to just `"pulse"`/
`"default"` was ruled out since it would defeat the app's actual multi-output-routing feature. This
heuristic is based on ALSA's documented naming convention rather than exhaustive real-device
coverage and may need tuning; `OutputScanner` logs every raw pre-filter device name at Debug level
specifically so it can be retuned from a user's log file alone. WASAPI shared-mode
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

`MediaSession.SetSpeed` calls `Seek(CurrentTime)` — a real, full reseek (seek-generation bump,
packet-channel drain, `av_seek_frame`, video flush, session Pause/Play), not a lighter path. A
**narrow flush** (skip the `av_seek_frame`, just discard+re-decode each `AudioPipeline`'s buffered
PCM at the new rate) looks like the obviously-cheaper design and was tried — it's wrong, confirmed
by testing, not a style preference. The demuxer routinely reads *ahead* of actual playback position
(`ThrottleDemuxAhead`/`MaxDemuxLookaheadSeconds` keep a lookahead margin of pre-fetched packets) —
narrow-flush clears each pipeline's decoded/buffered PCM and its packet channel, but never touches
the demuxer's read position, so the next packets handed to audio are whatever was already
pre-fetched, several seconds ahead of where playback actually is. `PumpToOutput` correctly refuses
to play a decoded chunk whose timestamp is that far ahead of the clock (`chunk.TimeSeconds >
delayedTargetSeconds + PumpWindowSeconds` → withheld, not played) and just sits silent until the
clock — ticking at the new speed, unaffected since video/clock were never flushed — walks forward
to catch up to that already-fetched content. Confirmed directly via real playback: video keeps
running instantly on a speed change, audio goes silent for several seconds. The full `Seek` avoids
this because `av_seek_frame` physically re-positions the demuxer to the target, which erases that
read-ahead margin as a side effect — there is currently no cheaper way to get that same
re-synchronization. (This becomes relevant again once a session can have multiple independent
demuxed sources — each `av_seek_frame` becomes its own network round-trip for a web source's
sidecar audio track — but that's unsolved future work, not a reason to reintroduce narrow-flush
today; don't re-attempt it without also fixing `PumpToOutput`'s ahead-of-target handling.)

That reseek runs under `_seekSync` (not a new lock): `AudioPipeline.Flush()` drains the pipeline's
single-reader `_decodedPcmChannel`, and `PumpToOutput` on the presentation thread reads that same
channel every iteration. `SetSpeed` is called from the UI thread, so without serializing against a
concurrent `Seek()` the two could touch the channel's reader side from two threads at once — the
channel opts into single-reader fast paths and isn't safe for that. This mirrors (and doesn't
widen) a race that already exists around `Seek`.

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

**A session can now be built from multiple independent input sources** — one primary (video,
optionally with its own audio) plus zero or more audio-only sidecar sources (`OMP.Lib/Session
/MediaInputSource.cs`), the shape a web source needs when yt-dlp exposes separate per-language
audio URLs instead of one muxed file. Local-file `Open` is unchanged — it's just the trivial
"one primary source, zero sidecars" case of the same `MediaSession` constructor, not a parallel
code path. Each `MediaInputSource` owns its own `AVFormatContext*`, its own `FormatSync` lock, its
own demux thread, and its own `EndOfStreamTracker` — session EOF is now the AND of every source's
tracker, combined with the existing `HasPendingPlayableContent()` tolerance. The mp3/wav/aac
PTS-baseline-anchor policy above generalizes to a per-source `IsSourceAudioOnly` check (true for
every sidecar unconditionally, and for the primary exactly when `_videoPipeline is null` — the
same expression as before, just asked once per source instead of once for the whole session) with
no new policy invented; each `MediaInputSource` keeps its own PTS-baseline-offset dictionary keyed
by *local* stream index, which is what actually fixes the id-collision bug a naive single shared
dictionary would have had (every sidecar's own stream 0 would otherwise collide as one key).
`Seek()` now loops over every source, seeking each independently and best-effort — one sidecar
failing to seek is logged and does not abort the others, matching the same "don't let one bad
source take down the rest" philosophy as the `AudioPipeline` construction try/catch in
`SetAudioRoutes`. A known, deliberately-accepted consequence: `SetSpeed` still calls
`Seek(CurrentTime)` (see Playback speed above), so a speed change on a session with N sidecars now
does N independent `av_seek_frame` calls — real cost for a web source, not yet solved, flagged
rather than hidden.

`AudioStream.Id`/`SubtitleStream.Id` are session-assigned surrogate ids, not raw FFmpeg stream
indices, precisely because a raw index is only unique *within one `AVFormatContext`* — two
different sidecar sources each reporting their own stream 0 would otherwise be indistinguishable.
`MediaSession.BuildAudioCatalog` remaps every source's locally-scanned streams to a global id and
records `(SourceId, LocalStreamIndex)` in `_audioStreamLocations`, consulted by `SetAudioRoutes`
before constructing an `AudioPipeline`. `AudioScanner`/`SubtitleScanner` themselves are unchanged —
still a correct, reusable "scan one context" unit. `AudioRouteMatcher` (persisted-preference
restore) needed zero changes, since it already matched by Title+Language, never by `Id`. A bare
sidecar CDN URL carries no container metadata worth trusting, so `AudioSidecarSource.Language`/
`Title` (supplied by the caller, who already has them from wherever the URL was resolved) are
preferred over whatever the scanner finds, rather than trusting an almost-certainly-absent tag.

## Threading

Playback worker threads (demux/audio/video/render) are identified by `PipelineWorkerRole`
(`OMP.Lib/Threading/PipelineWorkerRole.cs`), not raw strings — gives compile-time-checked
identity and doubles as a tag for future structured logging. Don't reach for empty
subclass-per-role types to get "stronger" identity than an enum already provides; that's
inheritance used for labeling, not behavior, and is the pattern to avoid here.

A session with sidecar sources runs N demux threads, not one — `PipelineWorker.Start` takes an
optional `threadName` override (defaulting to `role.ToString()` when omitted, so every other call
site is unaffected) so each source's demux thread gets a distinct `Thread.Name` (`"Demux-0"`,
`"Demux-1"`, ...) instead of N threads all named identically, which would otherwise make a
debugger/thread-dump session ambiguous about which thread belongs to which source.

`PlaybackClock` locks its state with a plain `Lock`/short critical section (field reads/writes,
no I/O, no blocking, no nested locks) — that's the correct, unremarkable use of a lock, not a
smell. Only worry about locking when critical sections do real work, block, or nest.

`ChannelExt` (`OMP.Lib/Extensions/ChannelExt.cs`) gives `ChannelWriter<T>`/`ChannelReader<T>` a
genuinely synchronous, thread-blocking `TryWriteBlocking`/`TryReadBlocking` (bool-returning, out
param for read) — `System.Threading.Channels` is async-only and has no native blocking API, and
this codebase's loops are OS threads, not async/await, so something has to bridge that. It's
sync-over-async (`WriteAsync(item, token).AsTask().GetAwaiter().GetResult()`), which isn't free
(forces a `Task` even on channel operations that would otherwise complete synchronously) —
`System.Collections.Concurrent
.BlockingCollection<T>` would avoid that allocation with a genuinely blocking implementation, but
doesn't support `BoundedChannelFullMode.DropOldest` (used by `MediaSession`'s audio packet
channel) without reimplementing that eviction by hand, so it's not a drop-in swap. Worth
revisiting only if profiling ever shows this allocation matters — unlikely, given the ffmpeg
decode work around every call site dominates. The `bool`/`out` shape (rather than swallowing
`OperationCanceledException` and returning `default!`) is deliberate: a silently-returned
default value is indistinguishable from a legitimately-default item, which is exactly the kind
of bug this shape avoids — see `PipelineWorker.TryWaitIfPaused()` for the same pattern.

**Use `GetAwaiter().GetResult()`, never `Task.Wait(CancellationToken)`, to bridge a cancellable
async operation.** `TryWriteBlocking`/`TryReadBlocking` originally did
`channelWriter.WriteAsync(item, token).AsTask().Wait(token)` — passing the *same* token to both
the async operation and the blocking `.Wait()` looks like it cancels the write, but `Wait(token)`
only makes the *wait* interruptible; it doesn't stop the underlying `WriteAsync` task, which keeps
running independently. Under real contention (`MediaSession.Dispose()` calling `Cancel()` while
the demux thread has a blocked `TryWriteBlocking` pending on a full video channel) this let
`Wait(token)` throw and return `false` — telling `DispatchClonedPacket` the packet was never
enqueued, so it called `av_packet_free` on it — while the abandoned `WriteAsync` task went on to
complete successfully moments later and actually placed that same packet in the channel, handing
the same freed pointer to whoever read it next. Confirmed via a real crash dump (`clrstack -all`
on a Linux CI crash showed the disposing thread blocked in `PipelineWorker.Join()` while the
demux thread was still natively inside `DispatchClonedPacket`'s `av_packet_free` call) — a genuine
double-free, not a timing-tolerance issue. `GetAwaiter().GetResult()` has no separate token
parameter: it only returns once the awaited operation itself reaches a terminal state, so there is
no "wait gave up early while the operation is still in flight" case left to race. It also
re-throws the operation's own exception directly (`OperationCanceledException`, not
`Task.Wait()`'s `AggregateException` wrapper), which is why the `catch` clause didn't need to
change. This is the general lesson, not just this one call site: never pass a `CancellationToken`
to both the operation you're starting *and* the mechanism you use to wait for it.

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

**`SliderHorizontalThumbWidth`/`Height` must never both be `0` at the same time as overriding
`SliderHorizontalHeight`** — confirmed by rendering to a real `RenderTargetBitmap` in a headless
test (see below), not guessed: with both thumb dimensions at `0` *and* a smaller
`SliderHorizontalHeight` present, the whole track — not just the thumb — renders with zero visual
height and the slider is completely invisible, even though `Slider.Bounds` still reports a normal,
non-zero size (the collapse happens deeper, in the `Track`/`RepeatButton` arrange pass, not at the
outer control). `SliderHorizontalThumbWidth="0"` alone (no `SliderHorizontalHeight` override) is
fine and is what actually makes a thumb invisible — a thumb has no width to draw a circle in.
`ProgressSlider`'s fix: `SliderHorizontalThumbWidth="0"` stays, but `SliderHorizontalThumbHeight`
is set to match the track height (currently `3`, not `0`) purely to keep that arrange pass
non-degenerate — the thumb is still invisible (zero width), this has no visible effect beyond
preventing the collapse.

Every other `Slider` in the app (main-window volume, `Options` audio routing rows, both flyouts —
anywhere the thumb stays visible) gets its knob size from **app-level** (not per-instance)
overrides in `App.axaml`: `SliderHorizontalThumbWidth`/`Height` = `14`, `SliderHorizontalHeight` =
`24`, `SliderThumbCornerRadius` = `7`, plus `SliderThumbBackground`/`SliderTrackValueFill` = the
accent color and a themed `SliderTrackFill` for the unfilled portion. `DynamicResource` lookups
fall through app-level resources exactly like any other ancestor scope, so this cascades to every
slider that doesn't declare its own `<Slider.Resources>` — `ProgressSlider`'s per-instance override
still wins locally over this app-level default, the same way instance styles always beat inherited
ones. One shared set of tokens instead of duplicating thumb-size XAML at every call site.

**Rendering an `AvaloniaFact` test to a real bitmap needs both `UseSkia()` and
`UseHeadlessDrawing = false`** on the shared `TestAppBuilder` (`OMP.Ui.Tests/TestAppBuilder.cs`) —
confirmed by testing: the default headless setup (`UseHeadlessDrawing` true, no explicit Skia
platform) *measures and arranges* controls correctly but never actually rasterizes anything, so
`RenderTargetBitmap.Render(window)` + `.Save(path)` silently produces nothing to look at (no
exception either). With both flags set, a `[AvaloniaFact]` test can `window.Show()`,
`Dispatcher.UIThread.RunJobs()`, render to a `RenderTargetBitmap`, and save a real PNG — this is
how the thumb-collapse bug above was actually found and confirmed, not guessed from reading XAML.
Verified this doesn't regress the existing Tier 1/2 suite (133 tests, ~500ms either way). Use this
technique for any future "is this actually visible / what does this actually look like" question
about a control template — it's far faster than reasoning about nested `ControlTemplate` XAML by
eye, and it's the only way that actually catches a rendering collapse like this one.

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
and text-parsing logic. It stays instant and dependency-free on purpose — anything needing a real
file or real FFmpeg native libs belongs in `OMP.Lib.IntegrationTests` instead, not here.

`OMP.Lib.IntegrationTests` opens real files from `test-fixtures/` (one of every supported
format, `71df4c1`, licensed per `test-fixtures/CREDITS.md`) through the real engine —
`MediaSessionRegistry`/`MediaSession` end to end, same construction path `OMP.Ui` uses (see
`NativeLibraryOptionsFactory`, a small local duplicate of `OMP.Ui/Services/FFmpegLibraryLocator`'s
macOS Homebrew probe — deliberately not shared, since this project should only depend on
`OMP.Lib`, not `OMP.Ui`). It bundles native libs the same RID-conditional way `OMP.Ui.csproj`
does (no macOS block — same reasoning: resolves against CI's `brew install ffmpeg@7` instead),
so it has to be run with `-r <rid>` (`dotnet test OMP.Lib.IntegrationTests
/OMP.Lib.IntegrationTests.csproj -r win-x64`, etc.), unlike `OMP.Lib.Tests`.

**No CI runner has a real audio output device**, confirmed by research rather than assumption:
Windows GitHub-hosted runners have none at all (the only known fix, a virtual driver called
Scream, needs certificate installation + `devcon` — too slow/fragile to be worth it here); macOS
runners are documented to *usually* get a "Null Audio Device" at boot but it's flaky (open
`actions/runner-images` issue); Linux has none by default either. Deliberately not provisioning
virtual devices anywhere to keep CI simple — so every audio-output-dependent assertion in
`PlaybackLifecycleTests` is written to adapt to however many real `AudioOutput`s exist at runtime
(`[SkippableFact]` + `Skip.If`, via the `Xunit.SkippableFact` package xUnit 2.x needs for
runtime-conditional skips): 0 outputs skips with a reason, ≥1 tests single-output volume, ≥2 tests
the actual multi-output routing feature. Open/seek/speed/video-frame-timing assertions need no
audio device at all and always run, since video pacing is wall-clock-driven, not slaved to a live
audio device (see the A/V-sync notes above) — this was the main reason the audio-hardware gap
turned out not to block most of the coverage that matters.

The `ci.yml` `integration-tests` job's `brew install ffmpeg@7` step on `macos-latest` is also the
only real, automated verification the macOS `FFmpegLibraryLocator`/`ffmpeg@7` path gets — genuine
signal on real (if ephemeral) macOS hardware, which is otherwise unavailable for this project.

Avalonia-side classes (`FullscreenController`, `VideoRenderSurface`, `WindowFactory`) are written
with plain constructor dependencies specifically so they're constructible with hand-written fakes
in `OMP.Ui.Tests` — see UI Testing below, which is now wired up.

## UI Testing

`OMP.Ui.Tests` splits into two tiers, the same "pure vs needs the real thing" split as
`OMP.Lib.Tests` vs `OMP.Lib.IntegrationTests`, just within one project since neither tier needs
native FFmpeg libs or a real display:

- **Tier 1** — plain `[Fact]`/`[Theory]`, no Avalonia runtime at all. Everything in `Extensions/`,
  `Helpers/`, `Input/`, the logic-bearing `Models/` (manual `INotifyPropertyChanged` classes),
  `Settings/` (`SubtitleZone`, `UserSettings` defaults, `UserSettingsJsonContext` round-trip), and
  `Services/MainWindowCommands` all land here — none of it touches a live `Window`/`Control`.
- **Tier 2** — `[AvaloniaFact]` (from `Avalonia.Headless.XUnit`), real control/window instances,
  no real display. Currently covers `Controls/SpeedFlyoutView`, `Controls/VolumeFlyoutView`, and
  `Services/FullscreenController`. `Windows/OptionsWindow` (now thin after extracting
  `OptionsSelector`), the other `Windows/*` dialogs, `SubtitleZoneEditorWindow`,
  `SubtitleOverlayRenderer`, and `MainWindow` itself are backlog, not yet covered.

**Deliberately excluded** — `SingleInstanceCoordinator`, `Services/FFmpegLibraryLocator`, and
`UserSettingsService` all hit hardcoded real OS paths (`Environment.SpecialFolder.ApplicationData`,
Homebrew prefixes) with no injection seam; testing them meaningfully needs a path-injection seam
first, which hasn't been added. `App.axaml.cs`/`Program.cs` are pure DI wiring, not tested directly.

**Mocking: `Moq`, with one deliberate exception.** `Moq` covers interfaces that are naturally
stateless/interaction-based (`IUserSettingsService`, `IWindowFactory`, `IMainWindowHotkeyService`).
`IMediaSession`/`IMediaSessionRegistry` get hand-written fakes instead
(`TestDoubles/FakeMediaSession.cs`, `FakeMediaSessionRegistry.cs`), because every `IMediaSession`
property is get-only and `Services/MainWindowCommands` relies on state coherence across calls
(`ApplySpeed` calls `session.SetSpeed(speed)` then immediately reads `session.Speed` back). Moq's
`SetupProperty`/`SetupAllProperties` need a *settable* property to auto-back a value, which
get-only interface members don't have — faithfully mocking this would mean a
`.Setup(s => s.SetSpeed(...)).Callback<double>(v => mock.Setup(s => s.Speed).Returns(v))` per
mutator/property pair, which is more code and more fragile (easy to add a new command and forget
the matching callback) than the ~90-line hand-written fake with ordinary auto-properties.

**`TestAppBuilder.cs` must set `App.Services` before any `[AvaloniaFact]` test runs, or every
single one fails identically.** Confirmed by testing, not assumed: `Avalonia.Headless.XUnit`'s
`HeadlessUnitTestSession` runs the *full* `AppBuilder.SetupUnsafe()` path, which calls both
`Initialize()` (loads `App.axaml`'s styles/resources — needed for any window/control's
`StaticResource` lookups to resolve) **and** `OnFrameworkInitializationCompleted()` — not just
`Initialize()` the way a naive reading of "headless test host" suggests. Since `App
.OnFrameworkInitializationCompleted` does `Services!.GetRequiredService<IUserSettingsService>()`,
an unset `Services` throws `ArgumentNullException` out of `AppBuilder.SetupUnsafe()` before any
test body runs, and every `[AvaloniaFact]` test in the assembly fails with the same opaque
stack trace. `TestAppBuilder` configures the **real** `App` type (needed for `App.axaml`'s
resources to load at all) with `.UseHeadless(...)`, then uses `.AfterSetup(builder => ((App)
builder.Instance!).Services = ...)` — the same hook `Program.cs` uses — to hand it a minimal
`IServiceProvider` stub that only resolves `IUserSettingsService`. `ApplicationLifetime` is not a
`IClassicDesktopStyleApplicationLifetime` under the headless unit-test session, so the
`desktop.MainWindow = Services!.GetRequiredService<MainWindow>()` branch never runs and the stub
doesn't need to resolve `MainWindow`.

**XAML-named fields are `internal` by default**, confirmed by reflecting on the compiled `OMP.dll`
rather than guessed — combined with `OMP.Ui/AssemblyInfo.cs`'s `[assembly:
InternalsVisibleTo("OMP.Ui.Tests")]`, this means a test can reach e.g. `speedFlyoutView.SpeedSlider`
or `speedFlyoutView.SpeedValueLabel` directly, with no visual-tree traversal needed, for any control
named directly in a `UserControl`/`Window`'s own XAML. Controls realized inside an `ItemsControl`
`DataTemplate` (e.g. `VolumeFlyoutView`'s per-row `RowVolumeSlider`) aren't fields on the outer
class at all — those still need `Avalonia.VisualTree`'s `GetVisualDescendants()`, matched by
`.Name`, after the container has actually been realized (see next point).

**Raise real routed pointer events directly on the control that subscribed the handler, rather than
simulating pixel-accurate mouse coordinates, when the test only cares about event sequencing.**
`control.RaiseEvent(new PointerPressedEventArgs(control, pointer, control, new Point(0, 0), 0, new
PointerPointProperties(), KeyModifiers.None, 1))` (with `pointer = new Avalonia.Input.Pointer(0,
PointerType.Mouse, isPrimary: true)`) reaches a `Tunnel`-registered handler even without the control
being attached to a shown `Window` — `RaiseEvent`'s route walks the `Parent` chain that already
exists once a control is added to its parent's `Children` via `InitializeComponent()`, regardless of
whether that subtree is ever shown. This is what the `SpeedFlyoutView`/`VolumeFlyoutView` drag-vs-
commit tests do, and it sidesteps needing a real layout pass or coordinate math entirely for tests
that don't need it. **`PointerCaptureLostEventArgs`'s 2-arg constructor is `[Obsolete]` in
`Avalonia` 11.3.11** ("might be removed in 12.0") — the stable replacement, and what a genuine
pointer release does under the hood, is `pointer.Capture(control); pointer.Capture(null);`, which
raises the real event through `IPointer` instead of hand-building the args.

**`TopLevel.LayoutManager` is not public** (confirmed via reflection — non-public getter), so a
test that needs to flush a pending layout pass (e.g. realizing an `ItemsControl`'s `DataTemplate`
containers after `SetOutputs(...)`, or picking up a `Height` change made after `Window.Show()`) can't
call `window.LayoutManager.ExecuteLayoutPass()` directly. Use `Avalonia.Threading.Dispatcher
.UIThread.RunJobs()` instead — it flushes the dispatcher queue, including the layout pass Avalonia
schedules at Render priority, and is what `FullscreenControllerTests`/`VolumeFlyoutViewTests` use.

`dotnet test OMP.Ui.Tests/OMP.Ui.Tests.csproj` needs no `-r <rid>` flag, unlike `OMP.Lib
.IntegrationTests` — nothing in either tier constructs a real `MediaSession` or loads native FFmpeg,
so there's no RID-dependent native content to resolve.
