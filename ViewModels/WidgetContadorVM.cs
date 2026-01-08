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
        // Mock init or Load from session if we had one
        NombreContador = "Contador Demo";
        Conectado = true;
    }

    [RelayCommand]
    public async Task CompletarTarea(Tarea tarea)
    {
        if (tarea == null) return;
        // Logic to complete task
        tarea.Estado = "completada";
        // Update DB
        // await _databaseService.UpdateTarea(tarea);
        Tareas.Remove(tarea);
    }

    [RelayCommand]
    public async Task ExpandirCentroControl()
    {
        // Navigate to Full Control Center
        var page = _serviceProvider.GetRequiredService<Views.PaginaCentroControlContador>();
            await App.Current.MainPage.Navigation.PushAsync(page);
    }
}
