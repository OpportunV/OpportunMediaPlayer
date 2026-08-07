using System.Diagnostics;

namespace OMP.Lib.Session;

internal sealed class PlaybackClock
{
    public double Speed { get; private set; } = 1.0;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _running;
            }
        }
    }

    public double CurrentSeconds
    {
        get
        {
            lock (_sync)
            {
                return CurrentSecondsUnlocked();
            }
        }
    }

    private readonly Lock _sync = new();
    private double _baseSeconds;
    private long _startTicks;
    private bool _running;

    public void Start()
    {
        lock (_sync)
        {
            if (_running)
            {
                return;
            }

            _startTicks = Stopwatch.GetTimestamp();
            _running = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _baseSeconds = CurrentSecondsUnlocked();
            _running = false;
        }
    }

    public void Rebase(double seconds)
    {
        lock (_sync)
        {
            _baseSeconds = seconds;
            _running = false;
        }
    }

    public void SetSpeed(double speed)
    {
        lock (_sync)
        {
            _baseSeconds = CurrentSecondsUnlocked();
            Speed = speed;
            if (_running)
            {
                _startTicks = Stopwatch.GetTimestamp();
            }
        }
    }

    private double CurrentSecondsUnlocked()
    {
        if (!_running)
        {
            return _baseSeconds;
        }

        var elapsed = Stopwatch.GetElapsedTime(_startTicks).TotalSeconds;
        return _baseSeconds + elapsed * Speed;
    }
}
