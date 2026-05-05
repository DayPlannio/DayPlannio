namespace DayPlannio.App.Views;

public partial class Clientes : ContentPage
{
	public Clientes()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		CustomTabBar.AbaAtual = "clientes";
	}

	private async void OnDeletarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Deleção", "Cliente deletado com sucesso!", "OK");
    }

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Edição", "Cliente editado com sucesso!", "OK");
    }

    private async void OnHistoricoClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Histórico", "Histórico do cliente exibido com sucesso!", "OK");
    }
         private async void OnNovoClienteClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Cliente", "Novo cliente adicionado com sucesso!", "OK");
    }

    private void OnBuscaTextChanged(object sender, TextChangedEventArgs e)
    {
    }
}