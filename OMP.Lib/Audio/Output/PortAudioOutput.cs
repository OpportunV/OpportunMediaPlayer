using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using OMP.Lib.Interop;
using PortAudioSharp;
using PortAudioStream = PortAudioSharp.Stream;

namespace OMP.Lib.Audio.Output;

internal sealed class PortAudioOutput : IAudioOutput
{
    public int PreferredSampleRate { get; }

    private readonly int _deviceIndex;
    private readonly ILogger _logger;

    private IWaveProvider? _source;
    private PortAudioStream? _stream;
    private byte[] _scratchBuffer = [];
    private int _bytesPerFrame;

    public PortAudioOutput(int deviceIndex, ILoggerFactory loggerFactory)
    {
        _deviceIndex = deviceIndex;
        _logger = loggerFactory.CreateLogger<PortAudioOutput>();

        PortAudioEnvironment.EnsureInitialized(_logger);

        PreferredSampleRate = (int)Math.Round(PortAudio.GetDeviceInfo(deviceIndex).defaultSampleRate);
    }

    public void Dispose()
    {
        _stream?.Dispose();
    }

    public void Init(IWaveProvider source)
    {
        _source = source;
        _bytesPerFrame = source.WaveFormat.BlockAlign;

        var suggestedLatency = PortAudio.GetDeviceInfo(_deviceIndex).defaultLowOutputLatency;
        var parameters = new StreamParameters
        {
            device = _deviceIndex,
            channelCount = source.WaveFormat.Channels,
            sampleFormat = SampleFormat.Int16,
            suggestedLatency = suggestedLatency,
        };

        var lowLatencyFrameCount = (uint)Math.Max(1, Math.Round(suggestedLatency * source.WaveFormat.SampleRate));

        PortAudioStream Open(uint framesPerBuffer) => new(
            inParams: null,
            outParams: parameters,
            sampleRate: source.WaveFormat.SampleRate,
            framesPerBuffer: framesPerBuffer,
            streamFlags: StreamFlags.ClipOff,
            callback: OnCallback,
            userData: null);

        try
        {
            _stream = Open(lowLatencyFrameCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to open PortAudio stream with an explicit {FrameCount}-frame buffer; " +
                "falling back to an unspecified buffer size.",
                lowLatencyFrameCount);

            _stream = Open(PortAudio.FramesPerBufferUnspecified);
        }

        PortAudioStreamInfo.LogNegotiatedLatency(_stream, _logger);
    }

    public void Play()
    {
        if (_stream is { IsStopped: true })
        {
            _stream.Start();
        }
    }

    public void Pause()
    {
        if (_stream is { IsActive: true })
        {
            _stream.Stop();
        }
    }

    private StreamCallbackResult OnCallback(
        IntPtr input,
        IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userDataPtr)
    {
        var byteCount = (int)frameCount * _bytesPerFrame;
        if (_scratchBuffer.Length < byteCount)
        {
            _scratchBuffer = new byte[byteCount];
        }

        var read = _source!.Read(_scratchBuffer, 0, byteCount);
        if (read < byteCount)
        {
            Array.Clear(_scratchBuffer, read, byteCount - read);
        }

        Marshal.Copy(_scratchBuffer, 0, output, byteCount);
        return StreamCallbackResult.Continue;
    }
}
