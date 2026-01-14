using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Panel.Models;
using Panel.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Panel.ViewModels;

public partial class PanelAdminVM : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private User? _usuarioLogueado;

    [ObservableProperty]
    private int _totalContadores;

    [ObservableProperty]
    private int _totalCompletadas;

    [ObservableProperty]
    private int _totalPendientes;

    [ObservableProperty]
    private string _eficiencia = "0%";

    // Report KPIs
    [ObservableProperty] private string _mejorContadorNombre = "N/A";
    [ObservableProperty] private string _mejorContadorEficiencia = "0%";
    [ObservableProperty] private string _promedioEficiencia = "0%";
    [ObservableProperty] private string _totalHorasEquipo = "0h";

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
    public ObservableCollection<ContadorReporte> ReportesDetallados { get; } = new();
    public ObservableCollection<KPIReporte> ReportesKPI { get; } = new();
    public ObservableCollection<User> TodosLosUsuarios { get; } = new();

    // User CRUD Form Properties
    [ObservableProperty] private string _nuevoUsuarioNombre = string.Empty;
    [ObservableProperty] private string _nuevoUsuarioUsername = string.Empty;
    [ObservableProperty] private string _nuevoUsuarioPassword = string.Empty;
    [ObservableProperty] private string _nuevoUsuarioRol = "Contador";
    [ObservableProperty] private string _nuevoUsuarioArea = "Ingresos";
    [ObservableProperty] private User? _usuarioEditando;
    [ObservableProperty] private bool _isEditMode = false;

    public List<string> Roles { get; } = new() { "Admin", "Contador" };
    public List<string> Areas { get; } = new() { "Admin", "Ingresos", "Egresos", "Declaraciones" };

    [ObservableProperty]
    private DateTime _fechaReporte = DateTime.Today;

    partial void OnFechaReporteChanged(DateTime value)
    {
        Task.Run(CargarDatos);
    }

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

    [ObservableProperty]
    private string _nuevaTareaCategoriaKPI = "Ingresos"; // Default

    public List<string> Prioridades { get; } = new() { "Prioritaria", "Variable" };
    public List<string> CategoriasKPI { get; } = new() { "Ingresos", "Egresos", "Declaraciones", "OpinionSAT", "EnvioPrevios" };

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

    public ICommand RefreshCommand => new Command(async () => await Refresh());

    public async Task Refresh()
    {
        IsBusy = true;
        await CargarDatos();
        IsBusy = false;
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

        // Cargar usuario actual desde sesión
        UsuarioLogueado = SessionService.CurrentUser;

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
        var todosUsuarios = await _databaseService.GetAllUsersAsync();

        MainThread.BeginInvokeOnMainThread(() => 
        {
            Contadores.Clear();
            foreach (var c in contadores) Contadores.Add(c);

            Tareas.Clear();
            foreach (var t in tareas) Tareas.Add(t);

            TodosLosUsuarios.Clear();
            foreach (var u in todosUsuarios) TodosLosUsuarios.Add(u);

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

            // Generar Reportes Detallados
            ReportesDetallados.Clear();
            foreach (var contador in contadores)
            {
                var tareasContador = tareas.Where(t => t.AsignadoAId == contador.Id).ToList();
                int total = tareasContador.Count;
                int completadas = tareasContador.Count(t => t.Estado == "completada");
                int pendientes = total - completadas;
                // Calculate stats
                double eficiencia = total > 0 ? (double)completadas / total : 0;
                decimal horas = tareasContador.Sum(t => t.TiempoReal);

                ReportesDetallados.Add(new ContadorReporte
                {
                    Contador = contador,
                    TotalTareas = total,
                    Completadas = completadas,
                    Pendientes = pendientes,
                    Eficiencia = eficiencia,
                    TiempoTrabajado = horas
                });
            }

            // Calcular KPIs por Categoría del Día Seleccionado (ACUMULATIVO MENSUAL)
            ReportesKPI.Clear();
            foreach (var cat in CategoriasKPI)
            {
                // 1. Universo: Tareas asignadas en el MES y AÑO de la fecha seleccionada
                var tareasMes = tareas.Where(t => 
                    t.CategoriaKPI == cat && 
                    t.FechaAsignacion.Month == FechaReporte.Month && 
                    t.FechaAsignacion.Year == FechaReporte.Year).ToList();

                int totalCat = tareasMes.Count;

                // 2. Completadas: Tareas del universo que se completaron en o antes de la fecha seleccionada
                // Esto crea el efecto acumulativo/progresivo día con día
                int compCat = tareasMes.Count(t => 
                    t.Estado == "completada" && 
                    t.FechaCompletado != null && 
                    t.FechaCompletado.Value.Date <= FechaReporte.Date);

                double porc = totalCat > 0 ? (double)compCat / totalCat : 0;

                ReportesKPI.Add(new KPIReporte
                {
                    Categoria = cat,
                    TotalTareas = totalCat,
                    Completadas = compCat,
                    Porcentaje = porc
                });
            }

            // Calcular KPIs Globales
            if (ReportesDetallados.Any())
            {
                var mejor = ReportesDetallados.OrderByDescending(r => r.Eficiencia).ThenByDescending(r => r.Completadas).First();
                MejorContadorNombre = mejor.Nombre;
                MejorContadorEficiencia = mejor.EficienciaTexto;

                double promedio = ReportesDetallados.Average(r => r.Eficiencia);
                PromedioEficiencia = $"{promedio:P0}";

                decimal totalHoras = ReportesDetallados.Sum(r => r.TiempoTrabajado);
                TotalHorasEquipo = $"{totalHoras:0.0}h";
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
            CategoriaKPI = NuevaTareaCategoriaKPI,
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
        NuevaTareaDescripcion = "";
        NuevaTareaPrioridad = "Variable";
        NuevaTareaCategoriaKPI = "Ingresos";
        
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
    public async Task ResetData()
    {
        bool confirm = await Application.Current!.MainPage!.DisplayAlert("Confirmar", "¿Borrar todas las tareas y mensajes? Esto es irreversible.", "Sí, Borrar", "Cancelar");
        if (!confirm) return;

        IsBusy = true;
        await _databaseService.ResetDatabaseAsync();
        await CargarDatos(); // Reload UI (will be empty)
        IsBusy = false;
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

    // ===================== USER CRUD COMMANDS =====================

    [RelayCommand]
    public async Task GuardarUsuario()
    {
        if (string.IsNullOrWhiteSpace(NuevoUsuarioNombre) || string.IsNullOrWhiteSpace(NuevoUsuarioUsername))
            return;

        User user;
        if (IsEditMode && UsuarioEditando != null)
        {
            // Edit existing user
            user = UsuarioEditando;
            user.Name = NuevoUsuarioNombre;
            user.Username = NuevoUsuarioUsername;
            if (!string.IsNullOrWhiteSpace(NuevoUsuarioPassword))
                user.Password = NuevoUsuarioPassword;
            user.Role = NuevoUsuarioRol;
            user.Area = NuevoUsuarioArea;
        }
        else
        {
            // Create new user
            user = new User
            {
                Name = NuevoUsuarioNombre,
                Username = NuevoUsuarioUsername,
                Password = NuevoUsuarioPassword,
                Role = NuevoUsuarioRol,
                Area = NuevoUsuarioArea,
                Estado = "desconectado"
            };
        }

        await _databaseService.SaveUserAsync(user);
        LimpiarFormularioUsuario();
        await CargarDatos();
    }

    [RelayCommand]
    public void IniciarEdicion(User user)
    {
        UsuarioEditando = user;
        NuevoUsuarioNombre = user.Name;
        NuevoUsuarioUsername = user.Username;
        NuevoUsuarioPassword = ""; // Don't show password
        NuevoUsuarioRol = user.Role;
        NuevoUsuarioArea = user.Area;
        IsEditMode = true;
    }

    [RelayCommand]
    public void CancelarEdicion()
    {
        LimpiarFormularioUsuario();
    }

    [RelayCommand]
    public async Task EliminarUsuario(User user)
    {
        if (user == null) return;
        await _databaseService.DeleteUserAsync(user);
        await CargarDatos();
    }

    [RelayCommand]
    public async Task SeleccionarFoto(User user)
    {
        if (user == null) return;

        try
        {
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Seleccionar foto de perfil"
            });

            if (result == null) return;

            // Create photos folder
            var destFolder = Path.Combine(FileSystem.AppDataDirectory, "fotos");
            Directory.CreateDirectory(destFolder);

            // Copy file
            var destPath = Path.Combine(destFolder, $"{user.Id}.jpg");
            using var source = await result.OpenReadAsync();
            using var dest = File.Create(destPath);
            await source.CopyToAsync(dest);

            // Update user
            user.FotoPerfil = destPath;
            await _databaseService.SaveUserAsync(user);
            await CargarDatos();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error selecting photo: {ex.Message}");
        }
    }

    private void LimpiarFormularioUsuario()
    {
        NuevoUsuarioNombre = string.Empty;
        NuevoUsuarioUsername = string.Empty;
        NuevoUsuarioPassword = string.Empty;
        NuevoUsuarioRol = "Contador";
        NuevoUsuarioArea = "Ingresos";
        UsuarioEditando = null;
        IsEditMode = false;
    }

    [ObservableProperty]
    private bool _isBusy;
}
