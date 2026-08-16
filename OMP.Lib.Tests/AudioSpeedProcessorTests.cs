using OMP.Lib.Audio;

namespace OMP.Lib.Tests;

public class AudioSpeedProcessorTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Process_AtNormalSpeed_IsByteForBytePassthrough()
    {
        var processor = new AudioSpeedProcessor();
        var source = MakeSamplePattern(frameCount: 32);

        var length = processor.Process(source, source.Length, speed: 1.0, SampleRate);

        Assert.Equal(source.Length, length);
        Assert.Equal(source, processor.AdjustedBuffer.AsSpan(0, length).ToArray());
    }

    [Fact]
    public void Process_DoubleSpeed_RoughlyHalvesFrameCount()
    {
        var processor = new AudioSpeedProcessor();
        var source = MakeSamplePattern(frameCount: 200);

        var length = processor.Process(source, source.Length, speed: 2.0, SampleRate);

        Assert.Equal(100 * 4, length);
    }

    [Fact]
    public void Process_HalfSpeed_RoughlyDoublesFrameCount()
    {
        var processor = new AudioSpeedProcessor();
        var source = MakeSamplePattern(frameCount: 100);

        var length = processor.Process(source, source.Length, speed: 0.5, SampleRate);

        Assert.Equal(200 * 4, length);
    }

    [Fact]
    public void Process_EmptySource_ReturnsZeroLength()
    {
        var processor = new AudioSpeedProcessor();

        var length = processor.Process([], 0, speed: 1.5, SampleRate);

        Assert.Equal(0, length);
    }

    [Fact]
    public void Process_GrowingBufferSizes_DoesNotCorruptSubsequentPassthroughCalls()
    {
        var processor = new AudioSpeedProcessor();

        var small = MakeSamplePattern(frameCount: 4);
        var smallLength = processor.Process(small, small.Length, speed: 1.0, SampleRate);
        Assert.Equal(small, processor.AdjustedBuffer.AsSpan(0, smallLength).ToArray());

        var large = MakeSamplePattern(frameCount: 4000);
        var largeLength = processor.Process(large, large.Length, speed: 1.0, SampleRate);
        Assert.Equal(large.Length, largeLength);
        Assert.Equal(large, processor.AdjustedBuffer.AsSpan(0, largeLength).ToArray());
    }

    private static byte[] MakeSamplePattern(int frameCount)
    {
        var buffer = new byte[frameCount * 4];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var left = (short)(frame % short.MaxValue);
            var right = (short)-(frame % short.MaxValue);
            var offset = frame * 4;
            BitConverter.GetBytes(left).CopyTo(buffer, offset);
            BitConverter.GetBytes(right).CopyTo(buffer, offset + 2);
        }

        return buffer;
    }
}
