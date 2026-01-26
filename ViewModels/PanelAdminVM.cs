using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Panel.Models;
using Panel.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Dispatching;

namespace Panel.ViewModels;

// Modelo para los iconos de etiquetas
public record IconoEtiqueta(string Name, string PathData);

public partial class PanelAdminVM : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private IDispatcherTimer? _alertasTimer;

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

        var msgs = msgsAll.GroupBy(m => m.Id).Select(g => g.First())
            .OrderBy(m => m.MarcaTiempo)
            .ToList();
        
        // Marcar primer mensaje de cada día
        DateTime? ultimaFecha = null;
        foreach (var m in msgs)
        {
            m.DeNombre = userMap.TryGetValue(m.De, out var de) ? de : $"ID:{m.De}";
            m.ParaNombre = userMap.TryGetValue(m.Para, out var para) ? para : $"ID:{m.Para}";
            m.EsMio = m.De == "admin";
            
            if (ultimaFecha == null || m.MarcaTiempo.Date != ultimaFecha.Value.Date)
            {
                m.EsPrimerMensajeDelDia = true;
                ultimaFecha = m.MarcaTiempo;
            }
            else
            {
                m.EsPrimerMensajeDelDia = false;
            }
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
        
        // Iniciar timer para revisión automática de alertas (cada 30 minutos)
        IniciarTimerAlertas();
    }

    private void IniciarTimerAlertas()
    {
        if (Application.Current?.Dispatcher == null) return;
        
        _alertasTimer = Application.Current.Dispatcher.CreateTimer();
        _alertasTimer.Interval = TimeSpan.FromMinutes(30);
        _alertasTimer.Tick += async (s, e) => await RevisarAlertasAutomaticas();
        _alertasTimer.Start();
        
        // Ejecutar una vez al iniciar
        Task.Run(RevisarAlertasAutomaticas);
    }

    [RelayCommand]
    public async Task RevisarAlertasAutomaticas()
    {
        try
        {
            var (vencimiento, retrasadas) = await _databaseService.EjecutarRevisionAlertasAsync();
            
            if (vencimiento > 0 || retrasadas > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Alertas generadas: {vencimiento} vencimiento, {retrasadas} retrasadas");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error revisando alertas: {ex.Message}");
        }
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

        // Crear un diccionario para mapear IDs a nombres
        var userMap = todosUsuarios.ToDictionary(u => u.Id, u => u.Name);

        MainThread.BeginInvokeOnMainThread(() => 
        {
            Contadores.Clear();
            foreach (var c in contadores) Contadores.Add(c);

            Tareas.Clear();
            foreach (var t in tareas)
            {
                // Asignar el nombre del usuario a la tarea
                if (userMap.TryGetValue(t.AsignadoAId, out var nombreUsuario))
                {
                    t.AsignadoANombre = nombreUsuario;
                }
                else
                {
                    t.AsignadoANombre = $"Usuario #{t.AsignadoAId}";
                }
                Tareas.Add(t);
            }

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
        await CargarPlantillas();
        await CargarEtiquetas();
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

        // Generar alerta automática de asignación
        await _databaseService.CrearAlertaAsignacionAsync(nueva, UsuarioLogueado?.Id ?? 0);

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
        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Confirmar", 
            "¿Borrar toda la base de datos?\n\nSe generará un backup automático antes de borrar.\n\nEsta acción afectará a todos los usuarios conectados.", 
            "Sí, Borrar", 
            "Cancelar");
        if (!confirm) return;

        IsBusy = true;
        
        try
        {
            // 1. Generar backup automático antes de borrar
            Console.WriteLine("[ADMIN] Generando backup antes de resetear base de datos...");
            var backupPath = await _databaseService.GenerarBackupCompletoAsync();
            
            // Descargar el backup automáticamente
            var result = await FileSaver.Default.SaveAsync(
                Path.GetFileName(backupPath),
                File.OpenRead(backupPath),
                CancellationToken.None);
            
            if (result.IsSuccessful)
            {
                Console.WriteLine($"[ADMIN] Backup guardado en: {result.FilePath}");
            }
            
            // 2. Enviar comando de reset a todos los clientes conectados
            if (_networkService != null && IsServerRunning)
            {
                var resetMessage = new SyncMessage
                {
                    Operation = SyncOperation.Delete,
                    EntityType = "ResetDatabase",
                    Sender = SessionService.CurrentIdentity,
                    Timestamp = DateTime.UtcNow
                };
                
                await _networkService.BroadcastMessageAsync(resetMessage);
                Console.WriteLine("[ADMIN] Comando de reset enviado a todos los clientes");
                
                // Esperar un momento para que los clientes procesen
                await Task.Delay(500);
            }
            
            // 3. Resetear base de datos local
            await _databaseService.ResetDatabaseAsync();
            await CargarDatos();
            
            await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Éxito", 
                $"Base de datos reseteada.\n\nBackup guardado en:\n{result.FilePath}", 
                "OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ADMIN] Error en ResetData: {ex.Message}");
            await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Error", 
                $"Error al resetear base de datos: {ex.Message}", 
                "OK");
        }
        finally
        {
            IsBusy = false;
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
            IsBusy = true;
            
            // Generar el backup JSON con todos los datos
            var backupPath = await _databaseService.GenerarBackupCompletoAsync();
            
            // Usar FileSavePicker para que el usuario elija dónde guardarlo
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("JSON Backup", new List<string>() { ".json" });
            savePicker.SuggestedFileName = $"Jazer_Backup_{DateTime.Now:yyyyMMdd_HHmmss}";

            var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window == null)
            {
                await Application.Current!.MainPage!.DisplayAlert("Error", "No se pudo acceder a la ventana actual.", "OK");
                IsBusy = false;
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // Copiar el archivo generado a la ubicación elegida
                File.Copy(backupPath, file.Path, true);
                
                // Leer el backup para mostrar estadísticas
                var backupContent = await File.ReadAllTextAsync(backupPath);
                var backupData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(backupContent);
                
                var usuarios = backupData.GetProperty("Usuarios").GetArrayLength();
                var tareas = backupData.GetProperty("Tareas").GetArrayLength();
                var mensajes = backupData.GetProperty("Mensajes").GetArrayLength();
                var alertas = backupData.GetProperty("Alertas").GetArrayLength();
                
                IsBusy = false;
                await Application.Current!.MainPage!.DisplayAlert(
                    "Backup Completo", 
                    $"Base de datos guardada en:\n{file.Path}\n\n" +
                    $"📊 Estadísticas del Backup:\n" +
                    $"• Usuarios: {usuarios}\n" +
                    $"• Tareas: {tareas}\n" +
                    $"• Mensajes: {mensajes}\n" +
                    $"• Alertas: {alertas}", 
                    "OK"
                );
            }
            else
            {
                IsBusy = false;
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
        switch (entityType)
        {
            case "Mensaje":
                await CargarMensajes();
                break;
            case "PlantillaTarea":
                await CargarPlantillas();
                break;
            case "Etiqueta":
            case "TareaEtiqueta":
                await CargarEtiquetas();
                if (TareaParaEtiquetas != null)
                    await AbrirEtiquetasDeTarea(TareaParaEtiquetas);
                break;
            case "Comentario":
                if (TareaParaComentarios != null)
                    await CargarComentariosDeTarea(TareaParaComentarios.Id);
                break;
            default:
                await CargarDatos();
                break;
        }
    }


    [RelayCommand]
    public async Task GuardarUsuario()
    {
        // Validación de campos obligatorios
        if (string.IsNullOrWhiteSpace(NuevoUsuarioNombre) || string.IsNullOrWhiteSpace(NuevoUsuarioUsername))
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert("Error", "El nombre y el usuario son obligatorios", "OK");
            return;
        }
        
        // Validación de longitud de username (mínimo 3, máximo 15)
        if (NuevoUsuarioUsername.Length < 3 || NuevoUsuarioUsername.Length > 15)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert("Error", "El usuario debe tener entre 3 y 15 caracteres", "OK");
            return;
        }
        
        // Validación de contraseña para nuevos usuarios
        if (!IsEditMode)
        {
            if (string.IsNullOrWhiteSpace(NuevoUsuarioPassword))
            {
                await Application.Current!.Windows[0].Page!.DisplayAlert("Error", "La contraseña es obligatoria para nuevos usuarios", "OK");
                return;
            }
            
            if (NuevoUsuarioPassword.Length < 6 || NuevoUsuarioPassword.Length > 20)
            {
                await Application.Current!.Windows[0].Page!.DisplayAlert("Error", "La contraseña debe tener entre 6 y 20 caracteres", "OK");
                return;
            }
        }
        
        // Validación de contraseña al editar (solo si se cambia)
        if (IsEditMode && !string.IsNullOrWhiteSpace(NuevoUsuarioPassword))
        {
            if (NuevoUsuarioPassword.Length < 6 || NuevoUsuarioPassword.Length > 20)
            {
                await Application.Current!.Windows[0].Page!.DisplayAlert("Error", "La contraseña debe tener entre 6 y 20 caracteres", "OK");
                return;
            }
        }

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
        
        await Application.Current!.Windows[0].Page!.DisplayAlert("Éxito", 
            IsEditMode ? "Usuario actualizado correctamente" : "Usuario creado correctamente", "OK");
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
        
        // Prevenir eliminación del usuario Admin
        if (user.Role == "Admin" || user.Username.ToLower() == "admin")
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert("Error", 
                "No se puede eliminar el usuario Administrador", "OK");
            return;
        }
        
        // Confirmación antes de eliminar
        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Confirmar", 
            $"¿Estás seguro de eliminar al usuario '{user.Name}' ({user.Username})?", 
            "Eliminar", "Cancelar");
            
        if (!confirm) return;
        
        await _databaseService.DeleteUserAsync(user);
        await CargarDatos();
        
        await Application.Current!.Windows[0].Page!.DisplayAlert("Éxito", 
            "Usuario eliminado correctamente", "OK");
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

            // Verificar tamaño del archivo original
            using (var sourceStream = await result.OpenReadAsync())
            {
                long fileSizeInBytes = sourceStream.Length;
                double fileSizeInMB = fileSizeInBytes / (1024.0 * 1024.0);
                
                if (fileSizeInMB > 10)
                {
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Imagen muy grande", 
                        $"La imagen seleccionada pesa {fileSizeInMB:F2} MB.\n\nPor favor selecciona una imagen menor a 10 MB.",
                        "OK"
                    );
                    return;
                }
                
                if (fileSizeInMB > 2)
                {
                    Console.WriteLine($"[FOTO] Imagen de {fileSizeInMB:F2} MB será comprimida");
                }
            }

            // Guardar y comprimir imagen
            var destFolder = Path.Combine(FileSystem.AppDataDirectory, "fotos");
            Directory.CreateDirectory(destFolder);
            var destPath = Path.Combine(destFolder, $"{user.Id}.jpg");
            
            await CompressAndSaveImageAsync(result, destPath);

            // Actualizar usuario localmente (skipSync para evitar enviar mensaje "User" duplicado)
            user.FotoPerfil = destPath;
            await _databaseService.SaveUserAsync(user, skipSync: true);
            
            // Leer imagen comprimida como Base64
            byte[] imageBytes = await File.ReadAllBytesAsync(destPath);
            double finalSizeInMB = imageBytes.Length / (1024.0 * 1024.0);
            Console.WriteLine($"[FOTO] Tamaño final para transmisión: {finalSizeInMB:F2} MB");
            
            string base64Image = Convert.ToBase64String(imageBytes);

            // Broadcast la foto a todos los clientes conectados
            if (_networkService != null && IsServerRunning)
            {
                var photoMessage = new SyncMessage
                {
                    Operation = SyncOperation.Update,
                    EntityType = "ProfilePhoto",
                    UserId = user.Id,
                    FileData = base64Image,
                    FileName = $"{user.Id}.jpg",
                    Sender = SessionService.CurrentIdentity
                };

                await _networkService.BroadcastMessageAsync(photoMessage);
                Console.WriteLine($"[ADMIN] Foto de {user.Username} enviada a todos los clientes");
            }
            
            await CargarDatos();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error selecting photo: {ex.Message}");
        }
    }

    // Método helper para comprimir y redimensionar imágenes
    private async Task CompressAndSaveImageAsync(FileResult photo, string destPath)
    {
        const int maxWidth = 512;
        const int maxHeight = 512;
        const int quality = 85;

        using var sourceStream = await photo.OpenReadAsync();
        
#if WINDOWS
        // En Windows usamos System.Drawing
        using var originalImage = System.Drawing.Image.FromStream(sourceStream);
        
        // Calcular nuevas dimensiones manteniendo aspecto
        int newWidth = originalImage.Width;
        int newHeight = originalImage.Height;
        
        if (newWidth > maxWidth || newHeight > maxHeight)
        {
            double ratioX = (double)maxWidth / newWidth;
            double ratioY = (double)maxHeight / newHeight;
            double ratio = Math.Min(ratioX, ratioY);
            
            newWidth = (int)(newWidth * ratio);
            newHeight = (int)(newHeight * ratio);
        }
        
        // Crear imagen redimensionada
        using var resizedImage = new System.Drawing.Bitmap(newWidth, newHeight);
        using var graphics = System.Drawing.Graphics.FromImage(resizedImage);
        
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
        
        // Guardar con compresión JPEG
        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
        var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
        encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
            System.Drawing.Imaging.Encoder.Quality, (long)quality);
        
        resizedImage.Save(destPath, jpegEncoder, encoderParams);
#else
        // En otras plataformas, guardar sin comprimir por ahora
        using var destStream = File.Create(destPath);
        await sourceStream.CopyToAsync(destStream);
#endif
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

    #region Plantillas de Tareas

    public ObservableCollection<PlantillaTarea> Plantillas { get; } = new();

    [ObservableProperty] private string _plantillaNombre = string.Empty;
    [ObservableProperty] private string _plantillaDescripcion = string.Empty;
    [ObservableProperty] private string _plantillaCategoriaKPI = "General";
    [ObservableProperty] private decimal _plantillaTiempoEstimado = 4;
    [ObservableProperty] private string _plantillaPrioridad = "Media";
    [ObservableProperty] private string _plantillaFrecuencia = "Manual";
    [ObservableProperty] private int _plantillaDiaEjecucion = 1;
    [ObservableProperty] private int _plantillaDiasAnticipacion = 7;
    [ObservableProperty] private string _plantillaChecklist = string.Empty;
    [ObservableProperty] private User? _plantillaAsignadoPorDefecto;
    [ObservableProperty] private PlantillaTarea? _plantillaSeleccionada;
    [ObservableProperty] private bool _isEditandoPlantilla;

    public List<string> Frecuencias { get; } = new() { "Manual", "Diaria", "Semanal", "Mensual", "Trimestral" };

    [RelayCommand]
    public async Task CargarPlantillas()
    {
        var plantillas = await _databaseService.GetAllPlantillasAsync();
        var usuarios = await _databaseService.GetAllUsersAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Plantillas.Clear();
            foreach (var p in plantillas)
            {
                var creador = usuarios.FirstOrDefault(u => u.Id == p.CreadorId);
                p.CreadorNombre = creador?.Name ?? "Sistema";

                var asignado = usuarios.FirstOrDefault(u => u.Id == p.AsignadoPorDefectoId);
                p.AsignadoNombre = asignado?.Name ?? "Sin asignar";

                Plantillas.Add(p);
            }
        });
    }

    [RelayCommand]
    public async Task GuardarPlantilla()
    {
        if (string.IsNullOrWhiteSpace(PlantillaNombre)) return;

        var plantilla = IsEditandoPlantilla && PlantillaSeleccionada != null
            ? PlantillaSeleccionada
            : new PlantillaTarea();

        plantilla.Nombre = PlantillaNombre;
        plantilla.DescripcionBase = PlantillaDescripcion;
        plantilla.CategoriaKPI = PlantillaCategoriaKPI;
        plantilla.TiempoEstimadoHoras = PlantillaTiempoEstimado;
        plantilla.Prioridad = PlantillaPrioridad;
        plantilla.Frecuencia = PlantillaFrecuencia;
        plantilla.DiaEjecucion = PlantillaDiaEjecucion;
        plantilla.DiasAnticipacion = PlantillaDiasAnticipacion;
        plantilla.Checklist = PlantillaChecklist;
        plantilla.AsignadoPorDefectoId = PlantillaAsignadoPorDefecto?.Id ?? 0;
        plantilla.CreadorId = UsuarioLogueado?.Id ?? 0;

        await _databaseService.SavePlantillaAsync(plantilla);

        LimpiarFormularioPlantilla();
        await CargarPlantillas();
    }

    [RelayCommand]
    public void EditarPlantilla(PlantillaTarea plantilla)
    {
        if (plantilla == null) return;

        IsEditandoPlantilla = true;
        PlantillaSeleccionada = plantilla;

        PlantillaNombre = plantilla.Nombre;
        PlantillaDescripcion = plantilla.DescripcionBase;
        PlantillaCategoriaKPI = plantilla.CategoriaKPI;
        PlantillaTiempoEstimado = plantilla.TiempoEstimadoHoras;
        PlantillaPrioridad = plantilla.Prioridad;
        PlantillaFrecuencia = plantilla.Frecuencia;
        PlantillaDiaEjecucion = plantilla.DiaEjecucion;
        PlantillaDiasAnticipacion = plantilla.DiasAnticipacion;
        PlantillaChecklist = plantilla.Checklist;

        var usuario = Contadores.FirstOrDefault(u => u.Id == plantilla.AsignadoPorDefectoId);
        PlantillaAsignadoPorDefecto = usuario;
    }

    [RelayCommand]
    public async Task EliminarPlantilla(PlantillaTarea plantilla)
    {
        if (plantilla == null) return;

        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Confirmar", $"¿Eliminar la plantilla '{plantilla.Nombre}'?", "Sí", "No");

        if (!confirm) return;

        await _databaseService.DeletePlantillaAsync(plantilla);
        await CargarPlantillas();
    }

    [RelayCommand]
    public async Task GenerarTareaDesdePlantilla(PlantillaTarea plantilla)
    {
        if (plantilla == null) return;

        var tarea = await _databaseService.GenerarTareaDesdePlantillaAsync(plantilla);

        await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Tarea Creada",
            $"Se creó la tarea:\n'{tarea.Titulo}'\n\nVencimiento: {tarea.FechaVencimiento:dd/MM/yyyy}",
            "OK");

        await CargarPlantillas();
        await CargarDatos();
    }

    [RelayCommand]
    public async Task GenerarTareasRecurrentes()
    {
        IsBusy = true;

        var tareasGeneradas = await _databaseService.GenerarTareasRecurrentesAsync();

        IsBusy = false;

        string mensaje = tareasGeneradas.Count > 0 
            ? $"Se generaron {tareasGeneradas.Count} tareas automáticamente."
            : "No hay tareas recurrentes para generar hoy.";

        await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Tareas Recurrentes", mensaje, "OK");

        await CargarDatos();
    }

    [RelayCommand]
    public void CancelarEdicionPlantilla()
    {
        LimpiarFormularioPlantilla();
    }

    private void LimpiarFormularioPlantilla()
    {
        IsEditandoPlantilla = false;
        PlantillaSeleccionada = null;
        PlantillaNombre = string.Empty;
        PlantillaDescripcion = string.Empty;
        PlantillaCategoriaKPI = "General";
        PlantillaTiempoEstimado = 4;
        PlantillaPrioridad = "Media";
        PlantillaFrecuencia = "Manual";
        PlantillaDiaEjecucion = 1;
        PlantillaDiasAnticipacion = 7;
        PlantillaChecklist = string.Empty;
        PlantillaAsignadoPorDefecto = null;
    }

    #endregion

    #region Etiquetas

    public ObservableCollection<Etiqueta> Etiquetas { get; } = new();
    public ObservableCollection<TareaEtiqueta> EtiquetasDeTareaActual { get; } = new();

    [ObservableProperty] private string _etiquetaNombre = string.Empty;
    [ObservableProperty] private string _etiquetaColorHex = "#3B82F6";
    [ObservableProperty] private string _etiquetaDescripcion = string.Empty;
    [ObservableProperty] private string _etiquetaIcono = "tag";
    [ObservableProperty] private Etiqueta? _etiquetaSeleccionada;
    [ObservableProperty] private bool _isEditandoEtiqueta;
    [ObservableProperty] private Tarea? _tareaParaEtiquetas;
    
    // PathData calculado para la vista previa
    public string EtiquetaIconoPathData => Converters.IconNameToPathConverter.IconPaths.TryGetValue(EtiquetaIcono, out var path) 
        ? path 
        : Converters.IconNameToPathConverter.IconPaths["tag"];

    partial void OnEtiquetaIconoChanged(string value)
    {
        OnPropertyChanged(nameof(EtiquetaIconoPathData));
    }

    public List<string> ColoresEtiquetas { get; } = new()
    {
        "#EF4444", "#F59E0B", "#10B981", "#3B82F6", "#8B5CF6",
        "#EC4899", "#6B7280", "#14B8A6", "#F97316", "#84CC16"
    };

    public List<IconoEtiqueta> IconosEtiquetas { get; } = new()
    {
        new("tag", "M9.568 3H5.25A2.25 2.25 0 0 0 3 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 0 0 5.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 0 0 9.568 3Z M6 6h.008v.008H6V6Z"),
        new("campana", "M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0"),
        new("dinero", "M12 6v12m-3-2.818.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"),
        new("nube", "M2.25 15a4.5 4.5 0 0 0 4.5 4.5H18a3.75 3.75 0 0 0 1.332-7.257 3 3 0 0 0-3.758-3.848 5.25 5.25 0 0 0-10.233 2.33A4.502 4.502 0 0 0 2.25 15Z"),
        new("advertencia", "M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"),
        new("trofeo", "M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35m0 0a6.772 6.772 0 0 1-3.044 0")
    };

    [RelayCommand]
    public async Task CargarEtiquetas()
    {
        var etiquetas = await _databaseService.GetEtiquetasConUsoAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Etiquetas.Clear();
            foreach (var e in etiquetas) Etiquetas.Add(e);
        });
    }

    [RelayCommand]
    public async Task GuardarEtiqueta()
    {
        if (string.IsNullOrWhiteSpace(EtiquetaNombre)) return;

        var etiqueta = IsEditandoEtiqueta && EtiquetaSeleccionada != null
            ? EtiquetaSeleccionada
            : new Etiqueta();

        etiqueta.Nombre = EtiquetaNombre;
        etiqueta.ColorHex = EtiquetaColorHex;
        etiqueta.Descripcion = EtiquetaDescripcion;
        etiqueta.Icono = EtiquetaIcono;
        etiqueta.CreadorId = UsuarioLogueado?.Id ?? 0;

        await _databaseService.SaveEtiquetaAsync(etiqueta);

        LimpiarFormularioEtiqueta();
        await CargarEtiquetas();
    }

    [RelayCommand]
    public void EditarEtiqueta(Etiqueta etiqueta)
    {
        if (etiqueta == null) return;

        IsEditandoEtiqueta = true;
        EtiquetaSeleccionada = etiqueta;

        EtiquetaNombre = etiqueta.Nombre;
        EtiquetaColorHex = etiqueta.ColorHex;
        EtiquetaDescripcion = etiqueta.Descripcion;
        EtiquetaIcono = etiqueta.Icono;
    }

    [RelayCommand]
    public async Task EliminarEtiqueta(Etiqueta etiqueta)
    {
        if (etiqueta == null) return;

        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Confirmar", $"¿Eliminar la etiqueta '{etiqueta.Nombre}'?", "Sí", "No");

        if (!confirm) return;

        await _databaseService.DeleteEtiquetaAsync(etiqueta);
        await CargarEtiquetas();
    }

    [RelayCommand]
    public void CancelarEdicionEtiqueta()
    {
        LimpiarFormularioEtiqueta();
    }

    [RelayCommand]
    public void SeleccionarIcono(IconoEtiqueta icono)
    {
        if (icono != null)
            EtiquetaIcono = icono.Name;
    }

    [RelayCommand]
    public void SeleccionarColor(string color)
    {
        if (!string.IsNullOrEmpty(color))
            EtiquetaColorHex = color;
    }

    private void LimpiarFormularioEtiqueta()
    {
        IsEditandoEtiqueta = false;
        EtiquetaSeleccionada = null;
        EtiquetaNombre = string.Empty;
        EtiquetaColorHex = "#3B82F6";
        EtiquetaDescripcion = string.Empty;
        EtiquetaIcono = "tag";
    }

    [RelayCommand]
    public async Task AbrirEtiquetasDeTarea(Tarea tarea)
    {
        if (tarea == null) return;

        TareaParaEtiquetas = tarea;
        var etiquetasTarea = await _databaseService.GetEtiquetasPorTareaAsync(tarea.Id);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            EtiquetasDeTareaActual.Clear();
            foreach (var te in etiquetasTarea) EtiquetasDeTareaActual.Add(te);

            // Marcar etiquetas ya asignadas
            foreach (var e in Etiquetas)
            {
                e.EstaSeleccionada = etiquetasTarea.Any(te => te.EtiquetaId == e.Id);
            }
        });
    }

    [RelayCommand]
    public async Task ToggleEtiquetaEnTarea(Etiqueta etiqueta)
    {
        if (etiqueta == null || TareaParaEtiquetas == null) return;

        if (etiqueta.EstaSeleccionada)
        {
            await _databaseService.RemoverEtiquetaDeTareaAsync(TareaParaEtiquetas.Id, etiqueta.Id);
        }
        else
        {
            await _databaseService.AsignarEtiquetaATareaAsync(TareaParaEtiquetas.Id, etiqueta.Id, UsuarioLogueado?.Id ?? 0);
        }

        etiqueta.EstaSeleccionada = !etiqueta.EstaSeleccionada;
        await CargarEtiquetas();
    }

    [RelayCommand]
    public void CerrarPanelEtiquetas()
    {
        TareaParaEtiquetas = null;
        foreach (var e in Etiquetas)
        {
            e.EstaSeleccionada = false;
        }
    }

    #endregion

    #region Comentarios en Tareas

    public ObservableCollection<Comentario> ComentariosTareaActual { get; } = new();

    [ObservableProperty] private Tarea? _tareaParaComentarios;
    [ObservableProperty] private string _nuevoComentarioContenido = string.Empty;
    [ObservableProperty] private int _totalComentariosTarea;

    [RelayCommand]
    public async Task AbrirComentariosDeTarea(Tarea tarea)
    {
        if (tarea == null) return;

        TareaParaComentarios = tarea;
        await CargarComentariosDeTarea(tarea.Id);
    }

    private async Task CargarComentariosDeTarea(string tareaId)
    {
        var comentarios = await _databaseService.GetComentariosPorTareaAsync(tareaId);
        var usuarios = await _databaseService.GetAllUsersAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ComentariosTareaActual.Clear();
            foreach (var c in comentarios)
            {
                var autor = usuarios.FirstOrDefault(u => u.Id == c.AutorId);
                c.AutorNombre = autor?.Name ?? "Sistema";
                c.AutorFoto = autor?.FotoPerfil ?? "";
                c.EsMio = c.AutorId == UsuarioLogueado?.Id;
                ComentariosTareaActual.Add(c);
            }
            TotalComentariosTarea = comentarios.Count;
        });
    }

    [RelayCommand]
    public async Task AgregarComentario()
    {
        if (string.IsNullOrWhiteSpace(NuevoComentarioContenido) || TareaParaComentarios == null)
            return;

        var comentario = new Comentario
        {
            TareaId = TareaParaComentarios.Id,
            AutorId = UsuarioLogueado?.Id ?? 0,
            Contenido = NuevoComentarioContenido,
            Tipo = "Comentario"
        };

        // Detectar menciones @usuario
        var usuarios = await _databaseService.GetAllUsersAsync();
        var menciones = new List<int>();
        foreach (var u in usuarios)
        {
            if (NuevoComentarioContenido.Contains($"@{u.Username}") || NuevoComentarioContenido.Contains($"@{u.Name}"))
            {
                menciones.Add(u.Id);
            }
        }

        if (menciones.Any())
        {
            comentario.Menciones = System.Text.Json.JsonSerializer.Serialize(menciones);
        }

        await _databaseService.SaveComentarioAsync(comentario);

        NuevoComentarioContenido = string.Empty;
        await CargarComentariosDeTarea(TareaParaComentarios.Id);
    }

    [RelayCommand]
    public async Task EliminarComentario(Comentario comentario)
    {
        if (comentario == null) return;

        // Solo autor o admin puede eliminar
        if (comentario.AutorId != UsuarioLogueado?.Id && UsuarioLogueado?.Role != "Admin")
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert("Error", "Solo puedes eliminar tus propios comentarios.", "OK");
            return;
        }

        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert("Confirmar", "¿Eliminar este comentario?", "Sí", "No");

        if (confirm)
        {
            await _databaseService.DeleteComentarioAsync(comentario);
            if (TareaParaComentarios != null)
                await CargarComentariosDeTarea(TareaParaComentarios.Id);
        }
    }

    #endregion
}
