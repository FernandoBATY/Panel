using Panel.ViewModels;
using Panel.Models;

namespace Panel.Views;

public partial class WidgetPage : ContentPage
{
    private CentroControlContadorVM? _viewModel;

    public WidgetPage()
    {
        InitializeComponent();
    }

    public void SetViewModel(CentroControlContadorVM viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private void OnOpenSystemClicked(object sender, EventArgs e)
    {
        // Disparar evento para que App.xaml.cs lo maneje
        App.Current?.HideWidgetAndShowMain();
    }

    private async void OnTareaCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Pequeño hack para obtener la Tarea desde el evento del CheckBox
        if (sender is CheckBox checkBox && checkBox.BindingContext is Tarea tarea && _viewModel != null)
        {
            // Solo actuar si el usuario lo marcó (evitar ciclos por binding inicial)
            if (e.Value && tarea.Estado != "completada")
            {
                await _viewModel.CompletarTareaCommand.ExecuteAsync(tarea);
            }
            else if (!e.Value && tarea.Estado == "completada")
            {
                await _viewModel.RevertirTareaCommand.ExecuteAsync(tarea);
            }
        }
    }
}
