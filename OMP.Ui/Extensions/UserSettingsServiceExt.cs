using System.Linq;
using OMP.Lib.Audio.Output;
using OMP.Ui.Settings;

namespace OMP.Ui.Extensions;

internal static class UserSettingsServiceExt
{
    extension(IUserSettingsService settings)
    {
        public void UpsertOutputVolumeSetting(AudioOutput output, double volumePercent, bool muted, double? delayMs = null)
        {
            var existing = settings.Current.OutputVolumes
                .FirstOrDefault(o => o.FriendlyName == output.FriendlyName);

            if (existing is null)
            {
                existing = new OutputVolumeSetting { FriendlyName = output.FriendlyName };
                settings.Current.OutputVolumes.Add(existing);
            }

            existing.Volume = volumePercent / 100;
            existing.Muted = muted;

            if (delayMs is not null)
            {
                existing.DelayMs = delayMs.Value;
            }
        }
    }
}
