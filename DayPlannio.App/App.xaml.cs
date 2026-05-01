using DayPlannio.App.Views;
using Microsoft.Extensions.DependencyInjection;

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
            Window w = new Window(new Login());
            w.Height = 600;
            w.Width = 400;
            return w;
        }
    }
}