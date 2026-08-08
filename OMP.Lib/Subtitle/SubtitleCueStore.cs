namespace OMP.Lib.Subtitle;

internal sealed class SubtitleCueStore
{
    private readonly List<SubtitleCue> _cues = [];
    private readonly HashSet<(string ZoneId, double Start, double End)> _knownCueKeys = [];
    private readonly Lock _sync = new();

    public void Add(SubtitleCue cue)
    {
        var key = (cue.ZoneId, cue.StartSeconds, cue.EndSeconds);

        lock (_sync)
        {
            if (!_knownCueKeys.Add(key))
            {
                return;
            }

            var insertIndex = _cues.FindLastIndex(existing => existing.StartSeconds <= cue.StartSeconds) + 1;
            _cues.Insert(insertIndex, cue);
        }
    }

    public IReadOnlyList<SubtitleCue> GetActive(double timeSeconds)
    {
        lock (_sync)
        {
            var active = new List<SubtitleCue>();

            foreach (var cue in _cues)
            {
                if (cue.StartSeconds > timeSeconds)
                {
                    break;
                }

                if (timeSeconds < cue.EndSeconds)
                {
                    active.Add(cue);
                }
            }

            return active;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _cues.Clear();
            _knownCueKeys.Clear();
        }
    }
}
