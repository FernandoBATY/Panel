namespace Panel.Views;

public partial class PaginaCentroControlContador : ContentPage
{
    public PaginaCentroControlContador(ViewModels.CentroControlContadorVM vm)
    {
        // Inyección de ViewModel y enlace de datos
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
            var platformWindow = window.Handler.PlatformView as Microsoft.UI.Xaml.Window;
            if (platformWindow != null)
            {
                // Forzar maximizado en escritorio Windows
                var presenter = platformWindow.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                presenter?.Maximize();
            }
        }
#endif
        if (BindingContext is ViewModels.CentroControlContadorVM vm)
        {
            // Cargar datos al mostrar la vista
            await vm.CargarTodosLosDatosCommand.ExecuteAsync(null);
        }
    }

    private void OnTareaCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.BindingContext is Models.Tarea tarea)
        {
            if (BindingContext is ViewModels.CentroControlContadorVM vm)
            {
                // Marcar tarea completada desde la UI
                vm.CompletarTareaCommand.Execute(tarea);
            }
        }
    }
}
