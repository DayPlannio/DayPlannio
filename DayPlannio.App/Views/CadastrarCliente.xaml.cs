using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class CadastrarCliente : ContentPage
{
    public CadastrarCliente()
    {
        InitializeComponent();

        BindingContext =
            new CadastrarClienteViewModel(
                this,
                Navigation);
    }
}