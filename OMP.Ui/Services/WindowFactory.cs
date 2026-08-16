using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace OMP.Ui.Services;

internal sealed class WindowFactory(IServiceProvider serviceProvider) : IWindowFactory
{
    public TWindow Create<TWindow>() where TWindow : Window => serviceProvider.GetRequiredService<TWindow>();
}
