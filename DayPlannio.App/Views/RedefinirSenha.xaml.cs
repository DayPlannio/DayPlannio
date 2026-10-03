using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class RedefinirSenha : ContentPage
{
    private readonly RedefinirSenhaViewModel _vm;

    public RedefinirSenha(string emailInicial = "")
    {
        InitializeComponent();
        _vm = new RedefinirSenhaViewModel(Navigation, emailInicial);
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LimparAviso();
    }
}