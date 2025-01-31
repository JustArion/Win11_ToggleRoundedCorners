namespace Dawn.Apps.ToggleRoundedCorners.Services;

using System.Windows;
using Interfaces;
using Microsoft.Extensions.DependencyInjection;

internal sealed class WindowService(IServiceProvider serviceProvider) : IWindowService
{
    public void ShowWindow<TWindow>() where TWindow : Window
    {
        var window = serviceProvider.GetRequiredService<TWindow>();
        window.Show();
    }
}