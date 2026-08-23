using System.Collections.Generic;
using System.Linq;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Ui.Settings;

namespace OMP.Ui.Extensions;

internal static class OutputVolumeExt
{
    extension(IReadOnlyList<AudioOutput> outputs)
    {
        public IEnumerable<(AudioOutput Output, OutputVolumeSetting Setting)> MatchSettings(
            IReadOnlyList<OutputVolumeSetting> settings) => settings
            .Select(setting => (Output: outputs.FirstOrDefault(o => o.FriendlyName == setting.FriendlyName), Setting: setting))
            .Where(pair => pair.Output is not null)
            .Select(pair => (pair.Output!, pair.Setting));
    }

    extension(IReadOnlyList<AudioRoute> routes)
    {
        public IEnumerable<(AudioOutput Output, double VolumePercent, bool Muted)> ToVolumeRows(
            IReadOnlyDictionary<int, OutputVolumeState> volumes) => routes.Select(route =>
        {
            var state = volumes.TryGetValue(route.Output.Id, out var s) ? s : new OutputVolumeState(1.0, false);
            return (route.Output, state.Volume * 100, state.Muted);
        });
    }
}
