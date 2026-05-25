using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class CustomTabBar : ContentView
{
    public CustomTabBar()
    {
        InitializeComponent();

        BindingContext =
            new CustomTabBarViewModel(
                Navigation);
    }
}