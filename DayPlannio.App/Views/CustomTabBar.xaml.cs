using Microsoft.Maui.Controls.Shapes;

namespace DayPlannio.App.Views;

public partial class CustomTabBar : ContentView
{
	public static string AbaAtual = "";

	public CustomTabBar()
	{
		InitializeComponent();
		AtualizarUI();
	}

	private void AtualizarUI()
	{
		SetTab(TabAgenda, AgendaLabel, AbaAtual == "agenda");
		SetTab(TabServicos, ServicosLabel, AbaAtual == "servicos");
		SetTab(TabClientes, ClientesLabel, AbaAtual == "clientes");
		SetTab(TabFinanceiro, FinanceiroLabel, AbaAtual == "financeiro");
		SetTab(TabPerfil, PerfilLabel, AbaAtual == "perfil");
	}

	private void SetTab(Border tab, Label label, bool ativo)
	{
		tab.BackgroundColor = ativo
			? Color.FromArgb("#81D5D2")
			: Colors.Transparent;

		tab.StrokeShape = new RoundRectangle
		{
			CornerRadius = 12
		};

		tab.Padding = new Thickness(10, 6);

		label.TextColor = ativo
			? Color.FromArgb("#00504E")
			: Color.FromArgb("#6E7978");
	}

	private async void OnAgendaTapped(object sender, EventArgs e)
	{
		AbaAtual = "agenda";
		AtualizarUI();
		await Application.Current.MainPage.Navigation.PushAsync(new Agendamentos());
	}

	private async void OnServicosTapped(object sender, EventArgs e)
	{
		AbaAtual = "servicos";
		AtualizarUI();
		await Application.Current.MainPage.Navigation.PushAsync(new Servicos());
	}

	private async void OnClientesTapped(object sender, EventArgs e)
	{
		AbaAtual = "clientes";
		AtualizarUI();
		await Application.Current.MainPage.Navigation.PushAsync(new Clientes());
	}

	private async void OnFinanceiroTapped(object sender, EventArgs e)
	{
		AbaAtual = "financeiro";
		AtualizarUI();
		await Application.Current.MainPage.Navigation.PushAsync(new Financeiro());
	}

	private async void OnPerfilTapped(object sender, EventArgs e)
	{
		AbaAtual = "perfil";
		AtualizarUI();
		await Application.Current.MainPage.Navigation.PushAsync(new MeuPerfil());
	}
}