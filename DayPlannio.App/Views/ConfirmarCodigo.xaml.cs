using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class ConfirmarCodigo : ContentPage
{
    public ConfirmarCodigo(string email)
    {
        InitializeComponent();

        BindingContext = new ConfirmarCodigoViewModel(
            email,
            Navigation);
    }
}