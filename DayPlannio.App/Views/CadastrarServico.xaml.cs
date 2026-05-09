namespace DayPlannio.App.Views;

public partial class CadastrarServico : ContentPage
{
    public CadastrarServico()
    {
        InitializeComponent();
    }

    private void OnCriarClicked(object sender, EventArgs e)
    {
    }

    private void OnCancelarClicked(object sender, EventArgs e)
    {
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}