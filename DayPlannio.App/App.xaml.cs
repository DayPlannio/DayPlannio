using DayPlannio.App.Views;

namespace DayPlannio.App
{
	public partial class App : Application
	{
		public App()
		{
			InitializeComponent();
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			var navigationPage = new NavigationPage(new Financeiro());

			Window w = new Window(navigationPage)
			{
				Height = 600,
				Width = 400
			};

			return w;
		}
	}
}