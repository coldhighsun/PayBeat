using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PayBeat.App.ViewModels;

/// <summary>
/// Base class for all view models; implements <see cref="INotifyPropertyChanged"/> and provides
/// an equality-guarded setter helper.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the specified property.
    /// </summary>
    /// <param name="name">Property name; inferred from the call site via <see cref="CallerMemberNameAttribute"/>.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises
    /// <see cref="PropertyChanged"/> only when the value actually changes.
    /// </summary>
    /// <typeparam name="T">Field type.</typeparam>
    /// <param name="field">Backing field reference.</param>
    /// <param name="value">New value to assign.</param>
    /// <param name="name">Property name; inferred from the call site via <see cref="CallerMemberNameAttribute"/>.</param>
    /// <returns><see langword="true"/> if the value changed; <see langword="false"/> if it was equal to the existing value.</returns>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}