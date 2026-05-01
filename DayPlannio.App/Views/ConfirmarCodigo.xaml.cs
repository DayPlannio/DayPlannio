using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Views;

public partial class ConfirmarCodigo : ContentPage
{

    public ConfirmarCodigo()
    {
        InitializeComponent();
    }

    private async void OnConfirmarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Confirmação", "Código confirmado com sucesso!", "OK");
        await Navigation.PushAsync(new Views.NovaSenha());
    }
}