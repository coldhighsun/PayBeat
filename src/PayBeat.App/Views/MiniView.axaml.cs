using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using PayBeat.App.ViewModels;

namespace PayBeat.App.Views;

/// <summary>
/// Minimal widget view showing only the formatted earnings amount with no extra chrome.
/// </summary>
public partial class MiniView : UserControl
{
    /// <summary>
    /// Scale of the progress fill; its horizontal factor is the workday progress.
    /// </summary>
    private readonly ScaleTransform _progressScale;

    /// <summary>
    /// View model whose progress is currently shown, or <see langword="null"/> when none is attached.
    /// </summary>
    private MainViewModel? _viewModel;

    /// <summary>
    /// Initializes a new instance of <see cref="MiniView"/> and loads the XAML component tree.
    /// </summary>
    public MiniView()
    {
        InitializeComponent();
        _progressScale = (ScaleTransform)ProgressFill.RenderTransform!;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateProgress();
    }

    /// <summary>
    /// Updates the progress fill when the view model's progress changes.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(MainViewModel.Progress))
        {
            UpdateProgress();
        }
    }

    /// <summary>
    /// Scales the progress fill to the current workday progress.
    /// </summary>
    private void UpdateProgress()
    {
        _progressScale.ScaleX = Math.Clamp(_viewModel?.Progress ?? 0.0, 0.0, 1.0);
    }
}
