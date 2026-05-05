namespace DayPlannio.App.Views;

public partial class Financeiro : ContentPage
{
	public Financeiro()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		CustomTabBar.AbaAtual = "financeiro";
	}
}