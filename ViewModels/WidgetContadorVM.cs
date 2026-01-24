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
    
    // Nuevas métricas para el widget
    [ObservableProperty]
    private int _tareasPendientes;
    
    [ObservableProperty]
    private int _tareasCompletadasHoy;
    
    [ObservableProperty]
    private int _tareasVencidas;
    
    [ObservableProperty]
    private DateTime _ultimaActualizacion = DateTime.Now;
    
    // Propiedades para controlar visibilidad de la vista vacía
    [ObservableProperty]
    private bool _tieneTareas;
    
    [ObservableProperty]
    private bool _noTieneTareas = true;

    public ObservableCollection<Tarea> Tareas { get; } = new();
    
    private User? _currentUser;

    public WidgetContadorVM(DatabaseService databaseService, IServiceProvider serviceProvider)
    {
        _databaseService = databaseService;
        _serviceProvider = serviceProvider;
        NombreContador = "Contador Demo";
        Conectado = true;
    }
    
    public void Init(User user)
    {
        _currentUser = user;
        NombreContador = user.Name;
        Conectado = user.Estado == "conectado";
        
        Task.Run(CargarDatos);
    }
    
    public async Task CargarDatos()
    {
        if (_currentUser == null) return;
        
        try
        {
            var tareas = await _databaseService.GetTareasPorUsuarioAsync(_currentUser.Id);
            var hoy = DateTime.Today;
            
            // Calcular métricas
            var pendientes = tareas.Where(t => t.Estado != "completada").ToList();
            TareasPendientes = pendientes.Count;
            TareasCompletadasHoy = tareas.Count(t => 
                t.Estado == "completada" && 
                t.FechaCompletado.HasValue && 
                t.FechaCompletado.Value.Date == hoy);
            TareasVencidas = pendientes.Count(t => t.FechaVencimiento.Date < hoy);
            
            // Cargar las primeras 5 tareas pendientes (ordenadas por urgencia)
            var tareasWidget = pendientes
                .OrderBy(t => t.FechaVencimiento)
                .ThenByDescending(t => t.Prioridad == "alta" || t.Prioridad == "urgente")
                .Take(5)
                .ToList();
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Tareas.Clear();
                foreach (var t in tareasWidget)
                {
                    Tareas.Add(t);
                }
                UltimaActualizacion = DateTime.Now;
                
                // Actualizar visibilidad de la vista vacía
                TieneTareas = Tareas.Count > 0;
                NoTieneTareas = Tareas.Count == 0;
            });
            
            // Cargar mensajes no leídos
            var mensajes = await _databaseService.GetMensajesPorUsuarioAsync(_currentUser.Id);
            MensajesNuevos = mensajes.Count(m => !m.Leido && m.Para == _currentUser.Id.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando datos widget: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CompletarTarea(Tarea tarea)
    {
        if (tarea == null) return;
        
        tarea.Estado = "completada";
        tarea.FechaCompletado = DateTime.Now;
        await _databaseService.UpdateTareaAsync(tarea);
        
        Tareas.Remove(tarea);
        TareasPendientes = Math.Max(0, TareasPendientes - 1);
        TareasCompletadasHoy++;
        
        // Recargar para obtener la siguiente tarea
        await CargarDatos();
    }

    [RelayCommand]
    public async Task ExpandirCentroControl()
    {
        var page = _serviceProvider.GetRequiredService<Views.PaginaCentroControlContador>();
        await App.Current.Windows[0].Page!.Navigation.PushAsync(page);
    }
}
