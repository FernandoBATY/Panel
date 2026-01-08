using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Panel.Models;
using Panel.Services;
using System.Collections.ObjectModel;
using Panel.Views;

namespace Panel.ViewModels;

public partial class CentroControlContadorVM : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;
    private readonly SyncService _syncService;
    private User? _currentUser;

    // Header Info
    [ObservableProperty] private string _nombreContador = "Contador";
    [ObservableProperty] private string _initials = "CO";
    [ObservableProperty] private bool _conectado;

    // Navigation and Tabs
    [ObservableProperty] private int _selectedTabIndex = 0; // 0:Tareas, 1:Alertas, 2:Mensajes, 3:Documentos

    [RelayCommand]
    private void SetTab(string indexStr)
    {
        if (int.TryParse(indexStr, out int index))
        {
            SelectedTabIndex = index;
        }
    }

    // Statistics
    [ObservableProperty] private int _totalTareas;
    [ObservableProperty] private int _tareasCompletadas;
    [ObservableProperty] private int _tareasEnProgreso;
    [ObservableProperty] private double _horasRegistradas;

    // Badges
    [ObservableProperty] private int _alertasCount;
    [ObservableProperty] private int _mensajesNuevosCount;
    [ObservableProperty] private bool _hasAlertas;
    [ObservableProperty] private bool _hasMensajes;

    // Collections
    public ObservableCollection<Tarea> Tareas { get; } = new();
    public ObservableCollection<Alerta> AlertasCriticas { get; } = new();
    public ObservableCollection<Alerta> Notificaciones { get; } = new();
    public ObservableCollection<Mensaje> Mensajes { get; } = new();
    public ObservableCollection<Documento> Documentos { get; } = new(); // For simple list
    public ObservableCollection<IGrouping<string, Documento>> DocumentosAgrupados { get; } = new(); // For grouped view

    // New Message Form
    [ObservableProperty] private string _nuevoMensajeContenido = "";
    [ObservableProperty] private int _caracteresMensaje = 0;
    
    // Server Connection
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _serverInfo = "No conectado";
    [ObservableProperty] private string _serverIpManual = "";

    public CentroControlContadorVM(DatabaseService databaseService, NetworkService networkService, SyncService syncService)
    {
        _databaseService = databaseService;
        _networkService = networkService;
        _syncService = syncService;

        _databaseService.SetSyncService(_syncService);
        _syncService.DataChanged += OnSyncDataChanged;
    }

    public void Init(User user)
    {
        _currentUser = user;
        NombreContador = $"{user.Name}";
        Initials = user.Name.Length >= 2 ? user.Name.Substring(0, 2).ToUpper() : "CO";
        Conectado = user.Estado == "conectado";
        
        Task.Run(CargarTodosLosDatos);

        ServerInfo = "Ingresa la IP del servidor para conectar";
    }

    [RelayCommand]
    public async Task CargarTodosLosDatos()
    {
        if (_currentUser == null) return;

        IsBusy = true;
        try
        {
            await CargarTareas();
            await CargarAlertas();
            await CargarMensajes();
            await CargarDocumentos();
            CalcularEstadisticas();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CargarTareas()
    {
        var tareas = await _databaseService.GetTareasPorUsuarioAsync(_currentUser!.Id);
        Tareas.Clear();
        foreach (var t in tareas) Tareas.Add(t);
    }

    private async Task CargarAlertas()
    {
        var alertas = await _databaseService.GetAlertasPorUsuarioAsync(_currentUser!.Id);
        
        AlertasCriticas.Clear();
        Notificaciones.Clear();
        
        foreach (var a in alertas)
        {
            if (a.Tipo == "ALERTA") AlertasCriticas.Add(a);
            else Notificaciones.Add(a);
        }

        AlertasCount = AlertasCriticas.Count(a => !a.Vista);
        HasAlertas = AlertasCount > 0;
    }

    private async Task CargarMensajes()
    {
        var mensajesAll = await _databaseService.GetMensajesPorUsuarioAsync(_currentUser!.Id);
        var contadores = await _databaseService.GetContadoresAsync();
        
        var userMap = contadores.ToDictionary(u => u.Id.ToString(), u => u.Username);
        userMap["admin"] = "Administrador";
        userMap["todos"] = "Todos";

        MainThread.BeginInvokeOnMainThread(() => 
        {
            Mensajes.Clear();
            foreach (var m in mensajesAll) 
            {
                m.EsMio = m.De == _currentUser!.Id.ToString();
                m.DeNombre = userMap.TryGetValue(m.De, out var de) ? de : $"ID:{m.De}";
                m.ParaNombre = userMap.TryGetValue(m.Para, out var para) ? para : $"ID:{m.Para}";
                Mensajes.Add(m);
            }
        });

        MainThread.BeginInvokeOnMainThread(() => 
        {
            MensajesNuevosCount = Mensajes.Count(m => !m.Leido && m.Para == _currentUser.Id.ToString());
            HasMensajes = MensajesNuevosCount > 0;
        });
    }

    private async Task CargarDocumentos()
    {
        var docs = await _databaseService.GetDocumentosPorUsuarioAsync(_currentUser!.Id);
        Documentos.Clear();
        foreach (var d in docs) Documentos.Add(d);
        
        // Logic for grouping would go here if we had Task info joined
    }

    private void CalcularEstadisticas()
    {
        TotalTareas = Tareas.Count;
        TareasCompletadas = Tareas.Count(t => t.Estado == "completada");
        TareasEnProgreso = Tareas.Count(t => t.Estado == "en-progreso");
        // HorasRegistradas logic would sum up tracked time
        HorasRegistradas = 23.5; // Placeholder/Mock
    }

    [RelayCommand]
    public async Task MarcarAlertaVista(Alerta alerta)
    {
        if (alerta != null && !alerta.Vista)
        {
            alerta.Vista = true;
            await _databaseService.UpdateAlertaAsync(alerta);
            await CargarAlertas(); // Refresh counts
        }
    }

    [RelayCommand]
    public async Task CompletarTarea(Tarea tarea)
    {
        if (tarea != null)
        {
            tarea.Estado = "completada";
            await _databaseService.UpdateTareaAsync(tarea);
            await CargarTodosLosDatos();
        }
    }

    [RelayCommand]
    public async Task EnviarMensaje()
    {
        if (string.IsNullOrWhiteSpace(NuevoMensajeContenido) || _currentUser == null) return;

        var mensaje = new Mensaje
        {
            De = _currentUser.Id.ToString(),
            Para = "admin",
            Contenido = NuevoMensajeContenido,
            Tipo = "MENSAJE",
            MarcaTiempo = DateTime.Now,
            Leido = false
        };

        await _databaseService.SaveMensajeAsync(mensaje);
        NuevoMensajeContenido = "";
        CaracteresMensaje = 0;
        await CargarMensajes();
    }

    [RelayCommand]
    public async Task ConectarAlServidor()
    {
        if (string.IsNullOrWhiteSpace(ServerIpManual)) return;

        try
        {
            ServerInfo = $"Conectando a {ServerIpManual}...";
            var connected = await _networkService.ConnectToServerAsync(ServerIpManual.Trim());

            if (connected)
            {
                IsConnected = true;
                ServerInfo = $"✓ Conectado a {ServerIpManual}";
            }
            else
            {
                ServerInfo = "✗ No se pudo conectar";
            }
        }
        catch (Exception ex)
        {
            ServerInfo = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CopiarIP()
    {
        if (!string.IsNullOrWhiteSpace(ServerIpManual))
        {
            await Clipboard.Default.SetTextAsync(ServerIpManual);
        }
    }

    [RelayCommand]
    public async Task CerrarSesion()
    {
        _networkService.Disconnect();
        IsConnected = false;
        SessionService.ClearSession();
        Application.Current!.MainPage = Application.Current.Handler!.MauiContext!.Services.GetRequiredService<Views.LoginPage>();
    }
    
    // UI Helper for text changed
    partial void OnNuevoMensajeContenidoChanged(string value)
    {
        CaracteresMensaje = value.Length;
    }
    
    private async void OnSyncDataChanged(object? sender, string entityType)
    {
        if (entityType == "Mensaje") await CargarMensajes();
        else await CargarTodosLosDatos();
    }

    [ObservableProperty]
    private bool _isBusy;
}
