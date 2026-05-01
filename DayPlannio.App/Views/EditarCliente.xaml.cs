namespace DayPlannio.App.Views;

public partial class EditarCliente : ContentPage
{
	public EditarCliente()
	{
		InitializeComponent();
	}

    private async void OnSalvarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Edição", "Cliente editado com sucesso!", "OK");
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Edição", "Edição cancelada.", "OK");
    }
}