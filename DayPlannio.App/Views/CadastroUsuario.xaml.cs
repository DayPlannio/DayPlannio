using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class CadastroUsuario : ContentPage
{
    public CadastroUsuario()
    {
        InitializeComponent();
        BindingContext = new CadastroUsuarioViewModel(Navigation);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetNavBarIsVisible(this, false);
    }
}