using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class ConfirmarCodigo : ContentPage
{
    private readonly ConfirmarCodigoViewModel _vm;

    public ConfirmarCodigo(string email)
    {
        InitializeComponent();

        _vm = new ConfirmarCodigoViewModel(email, Navigation);
        BindingContext = _vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Cancelar();
    }
}