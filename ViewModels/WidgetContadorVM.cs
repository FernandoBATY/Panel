using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Panel.Models;
using Panel.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace Panel.ViewModels;

public partial class WidgetContadorVM : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly IServiceProvider _serviceProvider;

    // Datos de cabecera del widget
    [ObservableProperty]
    private string _nombreContador = string.Empty;

    [ObservableProperty]
    private bool _conectado;

    [ObservableProperty]
    private int _mensajesNuevos;

    public ObservableCollection<Tarea> Tareas { get; } = new();

    public WidgetContadorVM(DatabaseService databaseService, IServiceProvider serviceProvider)
    {
        _databaseService = databaseService;
        _serviceProvider = serviceProvider;
        NombreContador = "Contador Demo";
        Conectado = true;
    }

    [RelayCommand]
    public async Task CompletarTarea(Tarea tarea)
    {
        if (tarea == null) return;
        tarea.Estado = "completada";
        Tareas.Remove(tarea);
    }

    [RelayCommand]
    public async Task ExpandirCentroControl()
    {
        var page = _serviceProvider.GetRequiredService<Views.PaginaCentroControlContador>();
            await App.Current.Windows[0].Page!.Navigation.PushAsync(page);
    }
}
