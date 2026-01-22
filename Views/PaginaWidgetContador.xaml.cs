using Panel.ViewModels;

namespace Panel.Views;

public partial class PaginaWidgetContador : ContentPage
{
	public PaginaWidgetContador(WidgetContadorVM viewModel)
	{
		// Widget ligero con su propio ViewModel
		InitializeComponent();
	    BindingContext = viewModel;
	}
}
