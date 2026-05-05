using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Views;

public partial class CadastroUsuario : ContentPage
{
    public CadastroUsuario()
    {
        InitializeComponent();
    }

    private async void OnCadastrarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Cadastro", "Usuário cadastrado com sucesso!", "OK");   
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Views.Login());
    }

    private void OnTelefoneTextChanged(object sender, TextChangedEventArgs e)
    {
    }
}