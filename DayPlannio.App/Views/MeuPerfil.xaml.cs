using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Views;

public partial class MeuPerfil : ContentPage
{

    public MeuPerfil()
    {
        InitializeComponent();
    }

    private async void OnSalvarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Perfil", "Perfil atualizado com sucesso!", "OK");
    }

    private async void OnSairClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Perfil", "Sessão encerrada com sucesso!", "OK");
        await Navigation.PushAsync(new Views.Login());
    }
}