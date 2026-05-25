using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class CadastrarServico : ContentPage
{
    public CadastrarServico()
    {
        InitializeComponent();

        BindingContext =
            new CadastrarServicoViewModel(
                this,
                Navigation);
    }
}