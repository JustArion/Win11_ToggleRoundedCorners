namespace Dawn.Apps.ToggleRoundedCorners.Tools.Commands;

using System.Windows.Input;

internal abstract class BaseCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public abstract void Execute(object? parameter);

    public abstract bool CanExecute(object? parameter);

    protected void OnCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}