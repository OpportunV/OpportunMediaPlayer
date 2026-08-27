using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace OMP.Ui.Services;

internal sealed class WindowFactory(IServiceProvider serviceProvider) : IWindowFactory
{
    /// <inheritdoc/>
    public TWindow Create<TWindow>() where TWindow : Window => serviceProvider.GetRequiredService<TWindow>();

    /// <inheritdoc/>
    public async Task ShowDialogAsync<TWindow>(Window owner, Action<TWindow> configure) where TWindow : Window
    {
        var window = Create<TWindow>();
        configure(window);
        await window.ShowDialog(owner);
    }

    /// <inheritdoc/>
    public async Task<TResult?> ShowDialogAsync<TWindow, TResult>(Window owner, Action<TWindow> configure)
        where TWindow : Window
    {
        var window = Create<TWindow>();
        configure(window);
        return await window.ShowDialog<TResult?>(owner);
    }
}
