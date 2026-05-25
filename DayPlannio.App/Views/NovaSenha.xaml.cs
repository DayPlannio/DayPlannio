using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class NovaSenha : ContentPage
{
    public NovaSenha(string email, string codigo)
    {
        InitializeComponent();
        BindingContext = new NovaSenhaViewModel(email, codigo, this);
    }
}