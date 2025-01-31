namespace Dawn.Apps.ToggleRoundedCorners.Controls;

using System.Windows;
using System.Windows.Input;
using Tools.Commands;

public partial class WindowBar
{
    public static readonly DependencyProperty ShowTitleProperty =
        DependencyProperty.Register(nameof(ShowTitle), typeof(bool), typeof(WindowBar), new PropertyMetadata(null));

    public static readonly DependencyProperty LeftContentProperty =
        DependencyProperty.Register(nameof(LeftContent), typeof(object), typeof(WindowBar), new PropertyMetadata(null));

    public static readonly DependencyProperty RightContentProperty =
        DependencyProperty.Register(nameof(RightContent), typeof(object), typeof(WindowBar), new PropertyMetadata(null));
    
    public static readonly DependencyProperty CanCloseProperty =
        DependencyProperty.Register(nameof(CanClose), typeof(bool), typeof(WindowBar), new PropertyMetadata(true));
    
    public static readonly DependencyProperty CanMaximizeProperty =
        DependencyProperty.Register(nameof(CanMaximize), typeof(bool), typeof(WindowBar), new PropertyMetadata(true));
    
    public static readonly DependencyProperty CanMinimizeProperty =
        DependencyProperty.Register(nameof(CanMinimize), typeof(bool), typeof(WindowBar), new PropertyMetadata(true));
    
    public WindowBar()
    {
        InitializeComponent();
        InitCommands();
    }

    public bool ShowTitle
    {
        get => (bool)GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    public object LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    public object RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public bool CanClose
    {
        get => (bool)GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }
    public bool CanMaximize
    {
        get => (bool)GetValue(CanMaximizeProperty);
        set => SetValue(CanMaximizeProperty, value);
    }
    public bool CanMinimize
    {
        get => (bool)GetValue(CanMinimizeProperty);
        set => SetValue(CanMinimizeProperty, value);
    }

    public ICommand? CloseCommand { get; private set; }
    public ICommand? MaximizeCommand { get; private set; }
    public ICommand? MinimizeCommand { get; private set; }

    private void InitCommands()
    {
        CloseCommand = new RelayCommand(CloseWindowExecuter);
        MaximizeCommand = new RelayCommand(MaximizeWindowExecuter);
        MinimizeCommand = new RelayCommand(MinimizeWindowExecuter);
    }

    private void MinimizeWindowExecuter(object? parameter)
    {
        if (parameter is not Window window)
            return;

        window.WindowState = WindowState.Minimized;
    }

    private void MaximizeWindowExecuter(object? parameter)
    {
        if (parameter is not Window window)
            return;

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindowExecuter(object? parameter)
    {
        if (parameter is not Window window)
            return;

        window.Close();
    }
}