namespace DayPlannio.App.Views;

public partial class EditarServico : ContentPage
{
    public EditarServico()
    {
        InitializeComponent();
    }

    private void OnSalvarClicked(object sender, EventArgs e)
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