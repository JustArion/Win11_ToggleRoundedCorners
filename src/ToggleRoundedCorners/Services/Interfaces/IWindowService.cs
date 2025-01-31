namespace Dawn.Apps.ToggleRoundedCorners.Services.Interfaces;

using System.Windows;

public interface IWindowService
{
    void ShowWindow<TWindow>() where TWindow : Window;
}