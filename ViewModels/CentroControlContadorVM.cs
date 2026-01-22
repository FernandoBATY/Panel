using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Panel.Models;
using Panel.Services;
using System.Collections.ObjectModel;
using Panel.Views;
using Microsoft.Maui.Dispatching;

namespace Panel.ViewModels;

public partial class CentroControlContadorVM : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;
    private readonly SyncService _syncService;
    
    [ObservableProperty]
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
    [ObservableProperty] private double _horasRegistradas; // Legacy
    [ObservableProperty] private string _tiempoSesion = "00:00:00"; // For UI Display
    
    // Timer
    private IDispatcherTimer? _timer;
    private DateTime _startTime;
    private int _tickCount = 0;

    // Badges
    [ObservableProperty] private int _alertasCount;
    [ObservableProperty] private int _mensajesNuevosCount;
    [ObservableProperty] private bool _hasAlertas;
    [ObservableProperty] private bool _hasMensajes;

    // Filter State
    [ObservableProperty] private string _filtroActual = "Todas"; // Todas, Pendientes, Completadas

    // Collections
    private List<Tarea> _allTareas = new(); // Master list
    public ObservableCollection<Tarea> Tareas { get; } = new(); // Display list (filtered)
    public ObservableCollection<Tarea> TareasPendientes { get; } = new(); // Widget list (always pending)
    
    public ObservableCollection<Alerta> AlertasCriticas { get; } = new();
    public ObservableCollection<Alerta> Notificaciones { get; } = new();
    public ObservableCollection<Mensaje> Mensajes { get; } = new();
    public ObservableCollection<Documento> Documentos { get; } = new(); 
    public ObservableCollection<IGrouping<string, Documento>> DocumentosAgrupados { get; } = new();

    // New Message Form (Manual implementation to handle character count)
    [ObservableProperty] private int _caracteresMensaje = 0;

    private string _nuevoMensajeContenido = "";
    public string NuevoMensajeContenido
    {
        get => _nuevoMensajeContenido;
        set
        {
            if (SetProperty(ref _nuevoMensajeContenido, value))
            {
                CaracteresMensaje = value.Length;
            }
        }
    }
    
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
        _networkService.MessageReceived += OnNetworkMessageReceived;
    }

    private void OnNetworkMessageReceived(object? sender, SyncMessage message)
    {
        if (message.Sender?.UserId == _currentUser?.Id) return; // Ignore self

        MainThread.BeginInvokeOnMainThread(async () => 
        {
            if (message.EntityType == "Tarea" && message.Operation == SyncOperation.Insert)
            {
                 // Check if for me? The message broadcast might be to all, but only relevant if assigned to me.
                 // Ideally payload has details, but SyncMessage is generic.
                 // We will fetch latest and check? Or just show generic "Nueva Tarea"?
                 // For now, let's assume if we get a Tarea create, check if we have a new one?
                 // Simpler: Just Reload and then check?
                 // Let's just show "Nueva actividad" or trigger reload.
                 // Actually, SyncService calls DataChanged which calls CargarTodos.
                 // We can hook into that.
                 // BUT user wants specific Toast.
                 // Let's trust the sync loop will load it, but we show toast immediately? 
                 // We don't know if it's for us without the object.
                 // However, DatabaseService Sync likely inserts it.
                 // Let's Query DB for last task?
                 
                 // Optimization: Just show "Se han actualizado las tareas" or wait for Cargar to finish and compare count?
                 
                 // BETTER: The message sender is Admin. 
                 MostrarNotificacionWindows("Nueva Actividad", "Se han actualizado las tareas/alertas.");
            }
            else if (message.EntityType == "Mensaje" && message.Operation == SyncOperation.Insert)
            {
                 // Check if for me
                 await CargarMensajes(); // Reload to be sure
                 // We rely on CargarMensajes updating counts
                 if (Mensajes.Any(m => !m.Leido && m.Para == _currentUser?.Id.ToString() && (DateTime.Now - m.MarcaTiempo).TotalSeconds < 10))
                 {
                     var msg = Mensajes.First(m => !m.Leido && m.Para == _currentUser?.Id.ToString());
                     MostrarNotificacionWindows($"Mensaje de {msg.DeNombre}", msg.Contenido);
                     
                     // Also refresh alerts to include this message
                     await CargarAlertas();
                 }
            }
        });
    }

    private void MostrarNotificacionWindows(string titulo, string contenido)
    {
#if WINDOWS
        try {
            var xml = $@"<toast>
                <visual>
                    <binding template='ToastGeneric'>
                        <text>{titulo}</text>
                        <text>{contenido}</text>
                    </binding>
                </visual>
            </toast>";
            
            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Show(toast);
        } catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"Error toast: {ex.Message}");
        }
#endif
    }

    public void Init(User user)
    {
        CurrentUser = user;
        NombreContador = $"{user.Name}";
        Initials = user.Name.Length >= 2 ? user.Name.Substring(0, 2).ToUpper() : "CO";
        Conectado = user.Estado == "conectado";
        
        Task.Run(CargarTodosLosDatos);

        ServerInfo = "Ingresa la IP del servidor para conectar";
        
        // Start Session Timer
        _startTime = DateTime.Now;
        if (Application.Current?.Dispatcher != null)
        {
            StartTimer();
        }
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
        
        _allTareas.Clear();
        _allTareas.AddRange(tareas);

        // Update Display List
        ActualizarListaTareas();

        // Update Widget List (Always Pendientes)
        TareasPendientes.Clear();
        foreach(var t in _allTareas.Where(t => t.Estado != "completada"))
        {
            TareasPendientes.Add(t);
        }
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

        // Merge Direct Messages into Notifications
        var mensajes = await _databaseService.GetMensajesPorUsuarioAsync(_currentUser!.Id);
        foreach(var m in mensajes.Where(x => !x.Leido && x.Para == _currentUser.Id.ToString()))
        {
            Notificaciones.Add(new Alerta { 
                Titulo = $"Mensaje de {m.De}", // We need name but fetching it is complex here without map. 
                Mensaje = m.Contenido, 
                Tipo = "MENSAJE", 
                FechaCreacion = m.MarcaTiempo,
                Vista = false
            });
        }
        
        // Sort by Date
        var sorted = new ObservableCollection<Alerta>(Notificaciones.OrderByDescending(n => n.FechaCreacion));
        Notificaciones.Clear();
        foreach(var n in sorted) Notificaciones.Add(n);

        AlertasCount = AlertasCriticas.Count(a => !a.Vista) + Notificaciones.Count(n => !n.Vista);
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
    private void AplicarFiltro(string filtro)
    {
        FiltroActual = filtro;
        ActualizarListaTareas();
    }

    private void ActualizarListaTareas()
    {
        Tareas.Clear();
        IEnumerable<Tarea> filtered = _allTareas;

        switch (FiltroActual)
        {
            case "Pendientes":
                filtered = _allTareas.Where(t => t.Estado != "completada");
                break;
            case "Completadas":
                filtered = _allTareas.Where(t => t.Estado == "completada");
                break;
            case "Todas":
            default:
                filtered = _allTareas;
                break;
        }

        foreach (var t in filtered) Tareas.Add(t);
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
            tarea.FechaCompletado = DateTime.Now;
            await _databaseService.UpdateTareaAsync(tarea);
            
            // Fix: Actualizar UI inmediatamente para el Widget
            TareasPendientes.Remove(tarea);
            
            // Refresh logic handled by widget or main view? 
            // Better to refresh all to keep consistency
             await CargarTodosLosDatos();
        }
    }

    [RelayCommand]
    public async Task RevertirTarea(Tarea tarea)
    {
        if (tarea != null)
        {
            tarea.Estado = "pendiente"; 
            tarea.FechaCompletado = null;
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
        // CaracteresMensaje is updated automatically by property setter
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
        
        // Fix: Asegurar tamaño completo al salir
        if (Application.Current is App app)
        {
             app.HideWidgetAndShowMain(); // Esto fuerza mostrar el Main y maximizarlo
        }
    }
    
    private async void OnSyncDataChanged(object? sender, string entityType)
    {
        if (entityType == "Mensaje") await CargarMensajes();
        else await CargarTodosLosDatos();
    }

    private void StartTimer()
    {
        if (_timer != null) return;
        
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (s, e) => await OnTimerTick();
        _timer.Start();
    }
    
    private async Task OnTimerTick()
    {
        var duration = DateTime.Now - _startTime;
        TiempoSesion = duration.ToString(@"hh\:mm\:ss");
        
        _tickCount++;
        
        // Send Heartbeat every 10 seconds if connected
        if (_tickCount % 10 == 0 && IsConnected)
        {
             await EnviarHeartbeat();
        }
    }
    
    private async Task EnviarHeartbeat()
    {
        if (_currentUser == null) return;
        
        try
        {
            var identity = SessionService.CurrentIdentity;
            if (identity != null)
            {
                identity.SessionDuration = TiempoSesion;
                
                var msg = new SyncMessage
                {
                     Operation = SyncOperation.Heartbeat,
                     Sender = identity,
                     EntityType = "Heartbeat",
                     Timestamp = DateTime.UtcNow
                };
                
                await _networkService.SendMessageAsync(msg);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending heartbeat: {ex.Message}");
        }
    }

    [ObservableProperty]
    private bool _isBusy;
}
