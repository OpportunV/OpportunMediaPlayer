namespace OMP.Ui.Settings;

public interface IUserSettingsService
{
    public UserSettings Current { get; }

    public void Save();
}
