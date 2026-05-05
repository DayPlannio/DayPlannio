namespace DayPlannio.App.Views;

public partial class Servicos : ContentPage
{
    public Servicos()
    {
        InitializeComponent();
    }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		CustomTabBar.AbaAtual = "servicos";
	}

	private void OnEditarClicked(object sender, EventArgs e)
    {
    }

    private void OnDeletarClicked(object sender, EventArgs e)
    {
    }

    private void OnNovoServicoClicked(object sender, EventArgs e)
    {
    }

    private void OnBuscaTextChanged(object sender, TextChangedEventArgs e)
    {
    }
}