using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class ExcluirFinanceiro : ContentPage
{
    public ExcluirFinanceiro(ExcluirFinanceiroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}