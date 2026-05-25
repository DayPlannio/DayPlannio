using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class EditarFinanceiro : ContentPage
{
    public EditarFinanceiro(EditarFinanceiroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}