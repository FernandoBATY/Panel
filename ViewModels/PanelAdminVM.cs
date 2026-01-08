using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Panel.Models;
using Panel.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Panel.ViewModels;

public partial class PanelAdminVM : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private int _totalContadores;

    [ObservableProperty]
    private int _totalCompletadas;

    [ObservableProperty]
    private int _totalPendientes;

    [ObservableProperty]
    private string _eficiencia = "0%";

    // Navegación
    [ObservableProperty] 
    private int _selectedTabIndex = 0; // 0:Dashboard, 1:Gestión, 2:Mensajes, 3:Reportes

    [RelayCommand]
    private void SetTab(string indexStr)
    {
        if (int.TryParse(indexStr, out int index)) SelectedTabIndex = index;
    }

    public ObservableCollection<User> Contadores { get; } = new();
    public ObservableCollection<Tarea> Tareas { get; } = new();

    // New Task Form Properties
    [ObservableProperty]
    private string _nuevaTareaTitulo = string.Empty;
    [ObservableProperty]
    private string _nuevaTareaDescripcion = string.Empty;
    [ObservableProperty]
    private DateTime _nuevaTareaVencimiento = DateTime.Now.AddDays(7);
    [ObservableProperty]
    private decimal _nuevaTareaHorasEstimadas = 8;
    [ObservableProperty]
    private User? _nuevaTareaContadorSeleccionado;
    [ObservableProperty]
    private string _nuevaTareaPrioridad = "Variable";

    public List<string> Prioridades { get; } = new() { "Prioritaria", "Variable" };

    // Network/Sync properties
    private readonly NetworkService _networkService;
    private readonly SyncService _syncService;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private string _connectionStatus = "Desconectado";

    [ObservableProperty]
    private string _serverIP = "";

    public ObservableCollection<NodeIdentity> ConnectedNodes { get; } = new();

    // Mensajería Admin
    public ObservableCollection<Mensaje> Mensajes { get; } = new();
    [ObservableProperty] private string _mensajeBroadcastContent = "";
    [ObservableProperty] private string _mensajeIndividualContent = "";
    [ObservableProperty] private User? _destinatarioMensaje;

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task EnviarMensajeIndividual()
    {
        if (string.IsNullOrWhiteSpace(MensajeIndividualContent) || DestinatarioMensaje == null) return;
        
        var msg = new Mensaje 
        { 
            De = "admin", 
            Para = DestinatarioMensaje.Id.ToString(), 
            Contenido = MensajeIndividualContent, 
            MarcaTiempo = DateTime.Now 
        };
        
        await _databaseService.SaveMensajeAsync(msg);
        MensajeIndividualContent = "";
        await CargarMensajes();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task EnviarBroadcast()
    {
        if (string.IsNullOrWhiteSpace(MensajeBroadcastContent)) return;
        
        var msg = new Mensaje 
        { 
            De = "admin", 
            Para = "todos", 
            Contenido = MensajeBroadcastContent, 
            Tipo = "NOTIFICACION",
            MarcaTiempo = DateTime.Now 
        };
        
        await _databaseService.SaveMensajeAsync(msg);
        MensajeBroadcastContent = "";
        await CargarMensajes();
    }

    private async Task CargarMensajes()
    {
        var msgsAll = await _databaseService.GetMensajesAsync();
        var contadores = await _databaseService.GetContadoresAsync();
        
        // Crear un mapa para búsqueda rápida
        var userMap = contadores.ToDictionary(u => u.Id.ToString(), u => u.Username);
        userMap["admin"] = "Administrador";
        userMap["todos"] = "Todos";

        // Filtrar duplicados por ID y resolver nombres
        var msgs = msgsAll.GroupBy(m => m.Id).Select(g => g.First()).ToList();
        
        foreach (var m in msgs)
        {
            m.DeNombre = userMap.TryGetValue(m.De, out var de) ? de : $"ID:{m.De}";
            m.ParaNombre = userMap.TryGetValue(m.Para, out var para) ? para : $"ID:{m.Para}";
        }

        MainThread.BeginInvokeOnMainThread(() => 
        {
            Mensajes.Clear();
            foreach (var m in msgs) Mensajes.Add(m);
        });
    }

    public PanelAdminVM(DatabaseService databaseService, NetworkService networkService, SyncService syncService)
    {
        _databaseService = databaseService;
        _networkService = networkService;
        _syncService = syncService;

        // Configurar eventos de red
        _networkService.ClientConnected += OnClientConnected;
        _networkService.ClientDisconnected += OnClientDisconnected;

        // Vincular SyncService con DatabaseService
        _databaseService.SetSyncService(_syncService);
        _syncService.DataChanged += OnSyncDataChanged;

        // Init (LoadData)
        Task.Run(CargarDatos);
    }

    private void OnClientConnected(object? sender, NodeIdentity identity)
    {
        ConnectedNodes.Add(identity);
        ConnectionStatus = $"Servidor activo - {ConnectedNodes.Count} conectados";
    }

    private void OnClientDisconnected(object? sender, string nodeId)
    {
        var node = ConnectedNodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node != null)
        {
            ConnectedNodes.Remove(node);
            ConnectionStatus = $"Servidor activo - {ConnectedNodes.Count} conectados";
        }
    }

    [RelayCommand]
    public async Task CargarDatos()
    {
        var contadores = await _databaseService.GetContadoresAsync();
        var tareas = await _databaseService.GetTareasAsync();

        MainThread.BeginInvokeOnMainThread(() => 
        {
            Contadores.Clear();
            foreach (var c in contadores) Contadores.Add(c);

            Tareas.Clear();
            foreach (var t in tareas) Tareas.Add(t);

            // Stats calculation
            TotalContadores = contadores.Count;
            TotalCompletadas = tareas.Count(t => t.Estado == "completada");
            TotalPendientes = tareas.Count(t => t.Estado == "pendiente");
            
            if (tareas.Count > 0)
            {
                double ef = (double)TotalCompletadas / tareas.Count * 100;
                Eficiencia = $"{ef:0}%";
            }
            else
            {
                Eficiencia = "N/A";
            }
        });

        await CargarMensajes();
    }

    [RelayCommand]
    public async Task CrearTarea()
    {
        if (string.IsNullOrWhiteSpace(NuevaTareaTitulo) || NuevaTareaContadorSeleccionado == null)
            return;

        var nueva = new Tarea
        {
            Titulo = NuevaTareaTitulo,
            Descripcion = NuevaTareaDescripcion,
            FechaVencimiento = NuevaTareaVencimiento,
            FechaAsignacion = DateTime.Now,
            TiempoEstimado = NuevaTareaHorasEstimadas,
            Estado = "pendiente",
            Prioridad = NuevaTareaPrioridad,
            AsignadoAId = NuevaTareaContadorSeleccionado.Id
        };
        
        // Save to DB
        await _databaseService.SaveTareaAsync(nueva);
        // Add to local list and update stats
        Tareas.Add(nueva);
        TotalPendientes++; 
        
        // Reset form
        NuevaTareaTitulo = "";
        NuevaTareaDescripcion = "";
        NuevaTareaPrioridad = "Variable";
        
        // Refresh full data to be safe (optional)
        await CargarDatos();
        await CargarMensajes(); // Refresh messages too because creating task might trigger logs if implemented
    }

    [RelayCommand]
    public async Task CopiarIP()
    {
        if (!string.IsNullOrWhiteSpace(ServerIP) && ServerIP != "No detectada")
        {
            await Clipboard.Default.SetTextAsync(ServerIP);
        }
    }

    [RelayCommand]
    public async Task IniciarServidor()
    {
        if (IsServerRunning) return;

        try
        {
            await _networkService.StartServerAsync();
            IsServerRunning = true;
            
            // Detectar IP de Tailscale
            var tailscaleIp = _networkService.GetTailscaleIP();
            ServerIP = tailscaleIp ?? "No detectada";
            
            ConnectionStatus = $"Servidor iniciado - Comparte esta IP: {ServerIP}";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CerrarSesion()
    {
        // Detener servidor/cliente
        _networkService.Disconnect();
        IsServerRunning = false;

        // Limpiar sesión
        SessionService.ClearSession();

        // Navegar a login
        Application.Current!.MainPage = Application.Current.Handler!.MauiContext!.Services.GetRequiredService<Views.LoginPage>();
    }

    private async void OnSyncDataChanged(object? sender, string entityType)
    {
        if (entityType == "Mensaje") await CargarMensajes();
        else await CargarDatos();
    }
}
