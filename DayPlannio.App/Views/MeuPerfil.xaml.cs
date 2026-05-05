using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Views;

public partial class MeuPerfil : ContentPage
{

    public MeuPerfil()
    {
        InitializeComponent();
    }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		CustomTabBar.AbaAtual = "perfil";
	}

	private async void OnSalvarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Perfil", "Perfil atualizado com sucesso!", "OK");
    }

    private async void OnSairClicked(object sender, EventArgs e)
    {
        bool confirmar = await DisplayAlertAsync(
            "Sair da conta",
            "Tem certeza que deseja encerrar a sessão?",
            "Sim",
            "Cancelar"
        );

        if (!confirmar)
            return;
        await Navigation.PushAsync(new Views.Login());
    }

    private void OnTelefoneTextChanged(object sender, TextChangedEventArgs e)
    {
    }
}