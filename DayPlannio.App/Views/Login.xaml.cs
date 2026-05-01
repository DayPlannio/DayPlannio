namespace DayPlannio.App.Views;

public partial class Login : ContentPage
{

    public Login()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Login", "Login realizado com sucesso!", "OK");
        Application.Current.MainPage = new AppShell();
    }

    private async void OnCadastroClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Views.CadastroUsuario());
    }

    private async void OnRedefinirSenha(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Views.RedefinirSenha());
    }
}