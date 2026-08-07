using Avalonia.Controls;

namespace OMP.Ui.Controls;

public interface IWindowFactory
{
    public TWindow Create<TWindow>() where TWindow : Window;
}
