namespace DayPlannio.App.Views;

public partial class ExcluirCliente : ContentPage
{
	public ExcluirCliente()
	{
		InitializeComponent();
	}

    private async void OnExcluirClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Exclusão", "Cliente excluído com sucesso!", "OK");
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Exclusão", "Operação cancelada!", "OK");
    }
}