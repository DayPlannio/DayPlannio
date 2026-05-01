namespace DayPlannio.App.Views;

public partial class CadastrarCliente : ContentPage
{
	public CadastrarCliente()
	{
		InitializeComponent();
	}

    private async void OnCriarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Cadastro", "Cliente cadastrado com sucesso!", "OK");
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Cancelar", "Cadastro cancelado!", "OK");
    }
}