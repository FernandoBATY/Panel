namespace Panel.Views;

public partial class PaginaCentroControlContador : ContentPage
{
    public PaginaCentroControlContador(ViewModels.CentroControlContadorVM vm)
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
        if (BindingContext is ViewModels.CentroControlContadorVM vm)
        {
            await vm.CargarTodosLosDatosCommand.ExecuteAsync(null);
        }
    }

    private void OnTareaCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.BindingContext is Models.Tarea tarea)
        {
            if (BindingContext is ViewModels.CentroControlContadorVM vm)
            {
                // Only execute if the new state matches logic (optional check)
                // We just want to trigger the update command
                vm.CompletarTareaCommand.Execute(tarea);
            }
        }
    }
}
