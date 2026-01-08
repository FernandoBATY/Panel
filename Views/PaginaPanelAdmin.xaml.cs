using Panel.ViewModels;

namespace Panel.Views;

public partial class PaginaPanelAdmin : ContentPage
{
	public PaginaPanelAdmin(ViewModels.PanelAdminVM vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        var window = this.Window;
        if (window != null)
        {
            // Maximizar en Windows
            var platformWindow = window.Handler.PlatformView as Microsoft.UI.Xaml.Window;
            if (platformWindow != null)
            {
                var presenter = platformWindow.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                presenter?.Maximize();
            }
        }
#endif
        if (BindingContext is PanelAdminVM vm)
        {
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
