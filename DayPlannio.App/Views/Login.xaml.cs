using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Login : ContentPage
{
    public Login()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel(Navigation);
    }
}