using Panel.ViewModels;

namespace Panel.Views;

public partial class PaginaPanelAdmin : ContentPage
{
	public PaginaPanelAdmin(ViewModels.PanelAdminVM vm)
    {
        try
        {
            // Inicializa la vista y captura fallos tempranos
            InitializeComponent();
            BindingContext = vm;
        }
        catch (Exception ex)
        {
            string logPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ADMIN_CTOR_CRASH.txt");
            System.IO.File.WriteAllText(logPath, ex.ToString());
            throw; 
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        var window = this.Window;
        if (window != null)
        {
            var platformWindow = window.Handler.PlatformView as Microsoft.UI.Xaml.Window;
            if (platformWindow != null)
            {
                // Abrir maximizado en Windows para vista de administración
                var presenter = platformWindow.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                presenter?.Maximize();
            }
        }
#endif
        if (BindingContext is PanelAdminVM vm)
        {
            // Cargar datos al entrar
            await vm.CargarDatosCommand.ExecuteAsync(null);
        }
    }

    private async void OnCopyIPClicked(object sender, EventArgs e)
    {
        if (BindingContext is ViewModels.PanelAdminVM vm && !string.IsNullOrEmpty(vm.ServerIP))
        {
            await Clipboard.SetTextAsync(vm.ServerIP);
            await DisplayAlert("Copiado", $"IP {vm.ServerIP} copiada al portapapeles", "OK");
        }
    }
}
