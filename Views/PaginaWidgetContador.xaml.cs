using Panel.ViewModels;

namespace Panel.Views;

public partial class PaginaWidgetContador : ContentPage
{
	public PaginaWidgetContador(WidgetContadorVM viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
	}
}
