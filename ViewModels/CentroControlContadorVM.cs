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
    // Servicios inyectados
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;
    private readonly SyncService _syncService;
    
    [ObservableProperty]
    private User? _currentUser;

    // Perfil y estado de conexión
    [ObservableProperty] private string _nombreContador = "Contador";
    [ObservableProperty] private string _initials = "CO";
    [ObservableProperty] private bool _conectado;

    // Navegación de pestañas
    [ObservableProperty] private int _selectedTabIndex = 0; 

    [RelayCommand]
    private void SetTab(string indexStr)
    {
        if (int.TryParse(indexStr, out int index))
        {
            SelectedTabIndex = index;
        }
    }

    // Métricas principales
    [ObservableProperty] private int _totalTareas;
    [ObservableProperty] private int _tareasCompletadas;
    [ObservableProperty] private int _tareasPendientesCount;
    [ObservableProperty] private double _horasRegistradas; 
    [ObservableProperty] private string _tiempoSesion = "00:00:00"; 
    
    private IDispatcherTimer? _timer;
    private DateTime _startTime;
    private int _tickCount = 0;

    // Alertas y mensajes
    [ObservableProperty] private int _alertasCount;
    [ObservableProperty] private int _mensajesNuevosCount;
    [ObservableProperty] private bool _hasAlertas;
    [ObservableProperty] private bool _hasMensajes;

    // Filtrado y colecciones
    [ObservableProperty] private string _filtroActual = "Todas"; 
    private List<Tarea> _allTareas = new();
    public ObservableCollection<Tarea> Tareas { get; } = new(); 
    public ObservableCollection<Tarea> TareasPendientes { get; } = new(); 
    
    public ObservableCollection<Alerta> AlertasCriticas { get; } = new();
    public ObservableCollection<Alerta> Notificaciones { get; } = new();
    public ObservableCollection<Mensaje> Mensajes { get; } = new();
    public ObservableCollection<Documento> Documentos { get; } = new(); 
    public ObservableCollection<IGrouping<string, Documento>> DocumentosAgrupados { get; } = new();

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
        if (message.Sender?.UserId == CurrentUser?.Id) return; // Ignore self

        MainThread.BeginInvokeOnMainThread(async () => 
        {
            if (message.EntityType == "Tarea" && message.Operation == SyncOperation.Insert)
            {
                 MostrarNotificacionWindows("Nueva Actividad", "Se han actualizado las tareas/alertas.");
            }
            else if (message.EntityType == "Mensaje" && message.Operation == SyncOperation.Insert)
            {
                 await CargarMensajes(); 
                 if (Mensajes.Any(m => !m.Leido && m.Para == _currentUser?.Id.ToString() && (DateTime.Now - m.MarcaTiempo).TotalSeconds < 10))
                 {
                     var msg = Mensajes.First(m => !m.Leido && m.Para == _currentUser?.Id.ToString());
                     MostrarNotificacionWindows($"Mensaje de {msg.DeNombre}", msg.Contenido);
                     
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

        ActualizarListaTareas();

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

        var mensajes = await _databaseService.GetMensajesPorUsuarioAsync(_currentUser!.Id);
        foreach(var m in mensajes.Where(x => !x.Leido && x.Para == _currentUser.Id.ToString()))
        {
            Notificaciones.Add(new Alerta { 
                Titulo = $"Mensaje de {m.De}",  
                Mensaje = m.Contenido, 
                Tipo = "MENSAJE", 
                FechaCreacion = m.MarcaTiempo,
                Vista = false
            });
        }
        
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
        
    }

    private void CalcularEstadisticas()
    {
        TotalTareas = _allTareas.Count; 
        TareasCompletadas = _allTareas.Count(t => t.Estado == "completada");
        TareasPendientesCount = _allTareas.Count(t => t.Estado == "pendiente"); 
        HorasRegistradas = 23.5;
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
            await CargarAlertas(); 
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
            
            TareasPendientes.Remove(tarea);
            
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
        if (string.IsNullOrWhiteSpace(NuevoMensajeContenido) || CurrentUser == null) return;

        var mensaje = new Mensaje
        {
            De = CurrentUser.Id.ToString(),
            Para = "admin",
            Contenido = NuevoMensajeContenido,
            Tipo = "MENSAJE",
            MarcaTiempo = DateTime.Now,
            Leido = false
        };

        await _databaseService.SaveMensajeAsync(mensaje);
        NuevoMensajeContenido = "";
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
        
        if (Application.Current is App app)
        {
             app.HideWidgetAndShowMain(); 
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
        
        if (_tickCount % 600 == 0 && IsConnected)
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
