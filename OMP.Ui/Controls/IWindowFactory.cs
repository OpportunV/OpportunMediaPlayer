using Avalonia.Controls;

namespace OMP.Ui.Controls;

internal interface IWindowFactory
{
    public TWindow Create<TWindow>() where TWindow : Window;
}
