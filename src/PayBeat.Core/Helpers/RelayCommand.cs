namespace PayBeat.Core.Helpers;

/// <summary>
/// Minimal <see cref="ICommand"/> implementation that delegates execution and availability
/// to caller-supplied callbacks.
/// </summary>
/// <param name="execute">Action invoked by <see cref="Execute"/>.</param>
/// <param name="canExecute">
/// Optional predicate invoked by <see cref="CanExecute"/>; returns <see langword="true"/> when omitted.
/// </param>
public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => execute();

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/> to notify the UI that command availability may have changed.
    /// </summary>
    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}