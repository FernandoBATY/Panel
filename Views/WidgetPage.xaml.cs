using Panel.ViewModels;
using Panel.Models;

namespace Panel.Views;

public partial class WidgetPage : ContentPage
{
    private CentroControlContadorVM? _viewModel;

    public WidgetPage()
    {
        // Vista alojada que muestra el widget compacto
        InitializeComponent();
    }

    public void SetViewModel(CentroControlContadorVM viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private void OnOpenSystemClicked(object sender, EventArgs e)
    {
        // Mostrar la app principal desde el widget
        App.Current?.HideWidgetAndShowMain();
    }

    private async void OnTareaCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.BindingContext is Tarea tarea && _viewModel != null)
        {
            if (e.Value && tarea.Estado != "completada")
            {
                // Completar tarea desde el widget
                await _viewModel.CompletarTareaCommand.ExecuteAsync(tarea);
            }
            else if (!e.Value && tarea.Estado == "completada")
            {
                // Revertir completado si se desmarca
                await _viewModel.RevertirTareaCommand.ExecuteAsync(tarea);
            }
        }
    }
}
