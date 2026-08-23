using Microsoft.Extensions.Logging;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Lib.Diagnostics;

internal sealed class LoopErrorLog(ILogger logger, string context, double intervalMs)
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private string? _lastMessage;
    private int _suppressedCount;

    public void Report(Exception ex)
    {
        if (ex.Message != _lastMessage)
        {
            _lastMessage = ex.Message;
            _suppressedCount = 0;
            _stopwatch.Restart();
            logger.LogError(ex, "{Context}", context);
            return;
        }

        _suppressedCount++;

        if (_stopwatch.ElapsedMilliseconds < intervalMs)
        {
            return;
        }

        logger.LogError(
            ex,
            "{Context} ({SuppressedCount} identical failure(s) suppressed).",
            context,
            _suppressedCount);
        _suppressedCount = 0;
        _stopwatch.Restart();
    }
}
