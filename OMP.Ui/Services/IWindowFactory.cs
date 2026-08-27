using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace OMP.Ui.Services;

public interface IWindowFactory
{
    /// <summary>
    /// Creates a new window.
    /// </summary>
    public TWindow Create<TWindow>() where TWindow : Window;

    /// <summary>
    /// Creates, configures, and shows a modal dialog that reports no result beyond having been closed.
    /// </summary>
    public Task ShowDialogAsync<TWindow>(Window owner, Action<TWindow> configure) where TWindow : Window;

    /// <summary>
    /// Creates, configures, and shows a modal dialog that closes with a result.
    /// </summary>
    public Task<TResult?> ShowDialogAsync<TWindow, TResult>(Window owner, Action<TWindow> configure)
        where TWindow : Window;
}
