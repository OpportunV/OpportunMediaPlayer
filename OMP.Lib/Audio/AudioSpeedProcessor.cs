using NAudio.Dsp;

namespace OMP.Lib.Audio;

internal sealed class AudioSpeedProcessor
{
    public byte[] AdjustedBuffer { get; private set; } = new byte[8192];

    private float[] _pitchLeftBuffer = [];
    private float[] _pitchRightBuffer = [];

    private readonly SmbPitchShifter _leftPitchShifter = new();
    private readonly SmbPitchShifter _rightPitchShifter = new();

    private const int BytesPerSampleFrame = 4;
    private const double SpeedEqualityEpsilon = 0.001;
    private const int MinFramesForPitchShift = 64;
    private const int PitchFftFrameSize = 1024;
    private const int PitchOversampling = 8;
    private const float Int16ToFloatScale = 32768f;
    private const float FloatToInt16Scale = 32767f;

    public int Process(byte[] source, int sourceBytes, double speed, int outputSampleRate)
    {
        var speedAdjustedBytes = AdjustPcmSpeed(source, sourceBytes, speed);
        ApplyPitchPreservingStretch(AdjustedBuffer, speedAdjustedBytes, speed, outputSampleRate);
        return speedAdjustedBytes;
    }

    private int AdjustPcmSpeed(byte[] source, int sourceBytes, double speed)
    {
        if (Math.Abs(speed - 1.0) < SpeedEqualityEpsilon)
        {
            EnsureSpeedBufferCapacity(sourceBytes);
            Buffer.BlockCopy(source, 0, AdjustedBuffer, 0, sourceBytes);
            return sourceBytes;
        }

        var sourceFrames = sourceBytes / BytesPerSampleFrame;
        if (sourceFrames == 0)
        {
            return 0;
        }

        var outputFrames = Math.Max(1, (int)Math.Round(sourceFrames / speed));
        var outputBytes = outputFrames * BytesPerSampleFrame;
        EnsureSpeedBufferCapacity(outputBytes);

        for (var i = 0; i < outputFrames; i++)
        {
            var sourceFrame = (int)Math.Min(sourceFrames - 1, Math.Floor(i * speed));
            Buffer.BlockCopy(
                source,
                sourceFrame * BytesPerSampleFrame,
                AdjustedBuffer,
                i * BytesPerSampleFrame,
                BytesPerSampleFrame);
        }

        return outputBytes;
    }

    private void EnsureSpeedBufferCapacity(int requiredSize)
    {
        if (AdjustedBuffer.Length >= requiredSize)
        {
            return;
        }

        AdjustedBuffer = new byte[requiredSize];
    }

    private void ApplyPitchPreservingStretch(byte[] pcmBuffer, int length, double speed, int outputSampleRate)
    {
        if (Math.Abs(speed - 1.0) < SpeedEqualityEpsilon)
        {
            return;
        }

        var frames = length / BytesPerSampleFrame;
        if (frames < MinFramesForPitchShift)
        {
            return;
        }

        if (_pitchLeftBuffer.Length < frames)
        {
            _pitchLeftBuffer = new float[frames];
            _pitchRightBuffer = new float[frames];
        }

        for (var i = 0; i < frames; i++)
        {
            var offset = i * BytesPerSampleFrame;
            _pitchLeftBuffer[i] = BitConverter.ToInt16(pcmBuffer, offset) / Int16ToFloatScale;
            _pitchRightBuffer[i] = BitConverter.ToInt16(pcmBuffer, offset + 2) / Int16ToFloatScale;
        }

        var pitchShift = (float)Math.Clamp(1.0 / speed, PlaybackSpeedLimits.Min, PlaybackSpeedLimits.Max);
        _leftPitchShifter.PitchShift(
            pitchShift,
            frames,
            PitchFftFrameSize,
            PitchOversampling,
            outputSampleRate,
            _pitchLeftBuffer);
        _rightPitchShifter.PitchShift(
            pitchShift,
            frames,
            PitchFftFrameSize,
            PitchOversampling,
            outputSampleRate,
            _pitchRightBuffer);

        for (var i = 0; i < frames; i++)
        {
            var offset = i * BytesPerSampleFrame;
            var left = (short)Math.Clamp(_pitchLeftBuffer[i] * FloatToInt16Scale, short.MinValue, short.MaxValue);
            var right = (short)Math.Clamp(_pitchRightBuffer[i] * FloatToInt16Scale, short.MinValue, short.MaxValue);

            pcmBuffer[offset] = (byte)(left & 0xff);
            pcmBuffer[offset + 1] = (byte)((left >> 8) & 0xff);
            pcmBuffer[offset + 2] = (byte)(right & 0xff);
            pcmBuffer[offset + 3] = (byte)((right >> 8) & 0xff);
        }
    }
}
