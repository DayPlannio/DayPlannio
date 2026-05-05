namespace DayPlannio.App.Views;

public partial class Agendamentos : ContentPage
{
	public Agendamentos()
	{
		InitializeComponent();

	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		CustomTabBar.AbaAtual = "agenda";
	}
}