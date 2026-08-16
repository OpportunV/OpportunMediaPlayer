using Avalonia.Controls;

namespace OMP.Ui.Services;

public interface IWindowFactory
{
    public TWindow Create<TWindow>() where TWindow : Window;
}
