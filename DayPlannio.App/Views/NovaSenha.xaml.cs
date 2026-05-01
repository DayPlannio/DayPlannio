using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Views;

public partial class NovaSenha : ContentPage
{

    public NovaSenha()
    {
        InitializeComponent();
    }

    private async void OnSalvarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Nova Senha", "Senha atualizada com sucesso!", "OK");
        await Navigation.PushAsync(new Views.Login());
    }
}