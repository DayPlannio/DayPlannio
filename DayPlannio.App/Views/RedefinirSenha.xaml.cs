using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class RedefinirSenha : ContentPage
{
    public RedefinirSenha()
    {
        InitializeComponent();
        BindingContext = new RedefinirSenhaViewModel(Navigation);
    }
}