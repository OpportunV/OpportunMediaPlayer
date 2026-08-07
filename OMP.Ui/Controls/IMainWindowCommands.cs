namespace OMP.Ui.Controls;

public interface IMainWindowCommands
{
    public void Attach(MainWindowCommandContext context);

    public void TogglePlayPause();

    public void StepBack();

    public void StepForward();

    public void IncreaseSpeed();

    public void DecreaseSpeed();

    public void ToggleFullscreen();

    public void ExitFullscreen();
}
