using DayPlannio.App.Views;

namespace DayPlannio.App
{
    public partial class App : Application
    {
        public static HttpClient HttpClient { get; } = new HttpClient
        {
#if ANDROID
            BaseAddress = new Uri("http://10.0.2.2:5143/")
#else
            BaseAddress = new Uri("http://localhost:5143/")
#endif
        };

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            Window w = new Window(new NavigationPage(new CadastroUsuario()));
            w.Height = 600;
            w.Width = 400;
            return w;
        }
    }
}