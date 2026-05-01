using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Views;

public partial class RedefinirSenha : ContentPage
{
    public RedefinirSenha()
    {
        InitializeComponent();
    }

    private async void OnRedefinirClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Redefinir Senha", "Instruções para redefinir a senha foram enviadas para o seu email!", "OK");
        await Navigation.PushAsync(new Views.ConfirmarCodigo());
    }
}