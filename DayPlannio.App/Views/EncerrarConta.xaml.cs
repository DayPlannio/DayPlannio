using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class EncerrarConta : ContentPage
{
    private readonly EncerrarContaViewModel _viewModel;

    public EncerrarConta(Action<bool> aoDecidir)
    {
        InitializeComponent();

        _viewModel = new EncerrarContaViewModel(
            Navigation,
            aoDecidir);

        BindingContext = _viewModel;
    }
}
