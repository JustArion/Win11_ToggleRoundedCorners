namespace Dawn.Apps.ToggleRoundedCorners.Tools.Commands;

using System.Windows.Input;

internal class RelayCommand : BaseCommand
{
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;

        CommandManager.RequerySuggested += (_, _) => RaiseCanExecuteChanged();
    }

    public override void Execute(object? parameter) => _execute(parameter);

    public override bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void RaiseCanExecuteChanged() => OnCanExecuteChanged();
}