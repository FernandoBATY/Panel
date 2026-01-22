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

    // Usuario logueado y métricas generales
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

    [ObservableProperty] private string _mejorContadorNombre = "N/A";
    [ObservableProperty] private string _mejorContadorEficiencia = "0%";
    [ObservableProperty] private string _promedioEficiencia = "0%";
    [ObservableProperty] private string _totalHorasEquipo = "0h";

    // Navegación por pestañas
    [ObservableProperty] 
    private int _selectedTabIndex = 0; 

    [RelayCommand]
    private void SetTab(string indexStr)
    {
        if (int.TryParse(indexStr, out int index)) SelectedTabIndex = index;
    }

    // Colecciones para UI
    public ObservableCollection<User> Contadores { get; } = new();
    public ObservableCollection<Tarea> Tareas { get; } = new();
    public ObservableCollection<ContadorReporte> ReportesDetallados { get; } = new();
    public ObservableCollection<KPIReporte> ReportesKPI { get; } = new();
    public ObservableCollection<User> TodosLosUsuarios { get; } = new();

    [ObservableProperty] private string _nuevoUsuarioNombre = string.Empty;
    [ObservableProperty] private string _nuevoUsuarioUsername = string.Empty;
    [ObservableProperty] private string _nuevoUsuarioPassword = string.Empty;
    [ObservableProperty] private string _nuevoUsuarioRol = "Contador";
    [ObservableProperty] private string _nuevoUsuarioArea = "Ingresos";
    [ObservableProperty] private User? _usuarioEditando;
    [ObservableProperty] private bool _isEditMode = false;

    public List<string> Roles { get; } = new() { "Admin", "Contador" };
    public List<string> Areas { get; } = new() { "Admin", "Ingresos", "Egresos", "Declaraciones" };

    // Filtros y parámetros de reportes
    [ObservableProperty]
    private DateTime _fechaReporte = DateTime.Today;

    partial void OnFechaReporteChanged(DateTime value)
    {
        Task.Run(CargarDatos);
    }

    // Creación de tareas
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
    private string _nuevaTareaCategoriaKPI = "Ingresos"; 

    public List<string> Prioridades { get; } = new() { "Prioritaria", "Variable" };
    public List<string> CategoriasKPI { get; } = new() { "Ingresos", "Egresos", "Declaraciones", "OpinionSAT", "EnvioPrevios" };

    // Servicios de red y sincronización
    private readonly NetworkService _networkService;
    private readonly SyncService _syncService;

    // Estado del servidor y red
    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private string _connectionStatus = "Desconectado";

    [ObservableProperty]
    private string _serverIP = "";

    public ObservableCollection<NodeIdentity> ConnectedNodes { get; } = new();

    // Mensajería
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
        
        var userMap = contadores.ToDictionary(u => u.Id.ToString(), u => u.Username);
        userMap["admin"] = "Administrador";
        userMap["todos"] = "Todos";

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

        _networkService.ClientConnected += OnClientConnected;
        _networkService.ClientDisconnected += OnClientDisconnected;
        _networkService.MessageReceived += OnNetworkMessageReceived;

        _databaseService.SetSyncService(_syncService);
        _syncService.DataChanged += OnSyncDataChanged;

        UsuarioLogueado = SessionService.CurrentUser;

        Task.Run(CargarDatos);
    }

    private void OnClientConnected(object? sender, NodeIdentity identity)
    {
        MainThread.BeginInvokeOnMainThread(() => 
        {
            ConnectedNodes.Add(identity);
            ConnectionStatus = $"Servidor activo - {ConnectedNodes.Count} conectados";
            
            var user = Contadores.FirstOrDefault(c => c.Id == identity.UserId);
            if (user != null)
            {
                user.Estado = "conectado";
                int idx = Contadores.IndexOf(user);
                if (idx != -1) Contadores[idx] = user; 
            }
        });
    }

    private void OnClientDisconnected(object? sender, string nodeId)
    {
        MainThread.BeginInvokeOnMainThread(() => 
        {
            var node = ConnectedNodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (node != null)
            {
                var user = Contadores.FirstOrDefault(c => c.Id == node.UserId);
                if (user != null)
                {
                    user.Estado = "desconectado";
                    user.SessionDuration = "00:00:00";
                    
                    int idx = Contadores.IndexOf(user);
                    if (idx != -1) Contadores[idx] = user;
                }

                ConnectedNodes.Remove(node);
                ConnectionStatus = $"Servidor activo - {ConnectedNodes.Count} conectados";
            }
        });
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

            ReportesDetallados.Clear();
            foreach (var contador in contadores)
            {
                var tareasContador = tareas.Where(t => t.AsignadoAId == contador.Id).ToList();
                int total = tareasContador.Count;
                int completadas = tareasContador.Count(t => t.Estado == "completada");
                int pendientes = total - completadas;
            
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

            ReportesKPI.Clear();
            foreach (var cat in CategoriasKPI)
            {
                var tareasMes = tareas.Where(t => 
                    t.CategoriaKPI == cat && 
                    t.FechaAsignacion.Month == FechaReporte.Month && 
                    t.FechaAsignacion.Year == FechaReporte.Year).ToList();

                int totalCat = tareasMes.Count;

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
        
        await _databaseService.SaveTareaAsync(nueva);

        Tareas.Add(nueva);
        TotalPendientes++; 
        
        NuevaTareaTitulo = "";
        NuevaTareaDescripcion = "";
        NuevaTareaDescripcion = "";
        NuevaTareaPrioridad = "Variable";
        NuevaTareaCategoriaKPI = "Ingresos";
        
        await CargarDatos();
        await CargarMensajes();
    }

    [RelayCommand]
    public async Task CopiarIP()
    {
        if (!string.IsNullOrWhiteSpace(ServerIP) && ServerIP != "No detectada")
        {
            await Clipboard.Default.SetTextAsync(ServerIP);
            await Application.Current!.Windows[0].Page!.DisplayAlert("Copiado", "La IP ya se copió al portapapeles", "OK");
        }
    }

    [RelayCommand]
    public async Task ResetData()
    {
        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert("Confirmar", "¿Borrar todas las tareas y mensajes? Esto es irreversible.", "Sí, Borrar", "Cancelar");
        if (!confirm) return;

        IsBusy = true;
        await _databaseService.ResetDatabaseAsync();
        await CargarDatos(); 
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
            
            var tailscaleIp = _networkService.GetTailscaleIP();
            ServerIP = tailscaleIp ?? "No detectada";
            
            ConnectionStatus = "Servidor iniciado y activo";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DescargarBackup()
    {
#if WINDOWS
        try
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("SQLite Database", new List<string>() { ".db3" });
            savePicker.SuggestedFileName = $"Jazer_Backup_{DateTime.Now:yyyyMMdd_HHmmss}";

            var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window == null)
            {
                await Application.Current!.MainPage!.DisplayAlert("Error", "No se pudo acceder a la ventana actual.", "OK");
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                IsBusy = true;
               
                string path = file.Path;

              
                
                try 
                {
                    if (File.Exists(path)) File.Delete(path);
                } 
                catch {  }

                await _databaseService.BackupDatabaseAsync(path);
                
                IsBusy = false;
                await Application.Current!.MainPage!.DisplayAlert("Backup Completo", $"Base de datos guardada en:\n{path}", "OK");
            }
        }
        catch (Exception ex)
        {
            IsBusy = false;
            await Application.Current!.Windows[0].Page!.DisplayAlert("Error", $"Fallo al crear backup: {ex.Message}", "OK");
        }
#else
        await Application.Current!.Windows[0].Page!.DisplayAlert("Info", "Esta función solo está disponible en Windows por el momento.", "OK");
#endif
    }

    [RelayCommand]
    public async Task CerrarSesion()
    {
        _networkService.Disconnect();
        IsServerRunning = false;

        SessionService.ClearSession();

        Application.Current!.Windows[0].Page = Application.Current.Handler!.MauiContext!.Services.GetRequiredService<Views.LoginPage>();
    }

    private async void OnSyncDataChanged(object? sender, string entityType)
    {
        if (entityType == "Mensaje") await CargarMensajes();
        else await CargarDatos();
    }


    [RelayCommand]
    public async Task GuardarUsuario()
    {
        if (string.IsNullOrWhiteSpace(NuevoUsuarioNombre) || string.IsNullOrWhiteSpace(NuevoUsuarioUsername))
            return;

        User user;
        if (IsEditMode && UsuarioEditando != null)
        {
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
        NuevoUsuarioPassword = ""; 
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

            var destFolder = Path.Combine(FileSystem.AppDataDirectory, "fotos");
            Directory.CreateDirectory(destFolder);

            var destPath = Path.Combine(destFolder, $"{user.Id}.jpg");
            using var source = await result.OpenReadAsync();
            using var dest = File.Create(destPath);
            await source.CopyToAsync(dest);

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

    private void OnNetworkMessageReceived(object? sender, SyncMessage message)
    {
        if (message.Operation == SyncOperation.Heartbeat && message.Sender != null)
        {
            MainThread.BeginInvokeOnMainThread(() => 
            {
                var node = ConnectedNodes.FirstOrDefault(n => n.NodeId == message.Sender.NodeId);
                if (node != null)
                {
                    node.SessionDuration = message.Sender.SessionDuration;
                }

                var user = Contadores.FirstOrDefault(c => c.Id == message.Sender.UserId);
                if (user != null)
                {
                     user.Estado = "conectado";
                     user.SessionDuration = message.Sender.SessionDuration;
                     
                     int idx = Contadores.IndexOf(user);
                     if (idx != -1)
                     {
                         Contadores[idx] = user; 
                     }
                }

                var reporte = ReportesDetallados.FirstOrDefault(r => r.Contador.Id == message.Sender.UserId);
                if (reporte != null)
                {
                    reporte.TiempoSesion = message.Sender.SessionDuration;
                    
                    int idx = ReportesDetallados.IndexOf(reporte);
                    if (idx != -1)
                    {
                        ReportesDetallados[idx] = reporte;
                    }
                }
            });
        }
    }

    [ObservableProperty]
    private bool _isBusy;
}
