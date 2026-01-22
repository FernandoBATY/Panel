using Panel.ViewModels;

namespace Panel.Views;

public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		// Inicializa la vista con el ViewModel de login
		InitializeComponent();
		BindingContext = viewModel;
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        var window = this.Window;
        if (window != null)
        {
            // Tamaño fijo de ventana para login en Windows
            window.Width = 400;
            window.Height = 550;
        }
#endif
    }
}
