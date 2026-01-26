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
    
    // ============================================
    // MÉTRICAS AVANZADAS (NUEVO)
    // ============================================
    [ObservableProperty] private int _tareasVencidas;
    [ObservableProperty] private int _tareasEstaSemana;
    [ObservableProperty] private double _porcentajeEficiencia;
    [ObservableProperty] private int _rachaProductividad;
    [ObservableProperty] private string _rachaTexto = "0 días";
    
    // ============================================
    // TAREAS DEL DÍA (MI DÍA)
    // ============================================
    public ObservableCollection<Tarea> TareasDelDia { get; } = new();
    [ObservableProperty] private bool _hayTareasDelDia;
    
    // ============================================
    // HISTORIAL DE ACTIVIDAD
    // ============================================
    public ObservableCollection<ActividadReciente> HistorialActividad { get; } = new();
    
    // ============================================
    // PROGRESO SEMANAL (GRÁFICA)
    // ============================================
    public ObservableCollection<ProgresoSemanalItem> ProgresoSemanal { get; } = new();
    [ObservableProperty] private int _maxTareasSemana = 1;
    
    // ============================================
    // NOTAS RÁPIDAS
    // ============================================
    [ObservableProperty] private string _notasRapidas = "";
    [ObservableProperty] private bool _notaGuardadaVisible = false;
    [ObservableProperty] private string _notaGuardadaMensaje = "";
    
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

    // ============================================
    // COMENTARIOS DE TAREAS
    // ============================================
    public ObservableCollection<Comentario> ComentariosTareaActual { get; } = new();
    
    [ObservableProperty] private Tarea? _tareaSeleccionadaParaComentarios;
    [ObservableProperty] private string _nuevoComentarioContenido = "";
    [ObservableProperty] private bool _mostrarPanelComentarios;
    [ObservableProperty] private int _comentariosCount;

    // ============================================
    // ETIQUETAS DE TAREAS
    // ============================================
    public ObservableCollection<Etiqueta> EtiquetasDisponibles { get; } = new();
    public ObservableCollection<Etiqueta> EtiquetasTareaActual { get; } = new();
    
    [ObservableProperty] private Etiqueta? _etiquetaFiltroSeleccionada;
    [ObservableProperty] private bool _filtrandoPorEtiqueta;

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
            // Solo mostrar notificaciones, no recargar datos aquí (OnSyncDataChanged lo hará)
            if (message.EntityType == "Tarea" && message.Operation == SyncOperation.Insert)
            {
                 MostrarNotificacionWindows("Nueva Actividad", "Se ha asignado una nueva tarea.");
            }
            else if (message.EntityType == "Mensaje" && message.Operation == SyncOperation.Insert)
            {
                 // Solo mostrar notificación, la recarga se hará en OnSyncDataChanged
                 var mensajesTemp = await _databaseService.GetMensajesPorUsuarioAsync(_currentUser?.Id ?? 0);
                 var mensajeNuevo = mensajesTemp.FirstOrDefault(m => !m.Leido && m.Para == _currentUser?.Id.ToString() && (DateTime.Now - m.MarcaTiempo).TotalSeconds < 10);
                 if (mensajeNuevo != null)
                 {
                     MostrarNotificacionWindows($"Mensaje de {mensajeNuevo.De}", mensajeNuevo.Contenido);
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
        
        Task.Run(async () => await CargarNotasGuardadas());
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
            await CargarEtiquetas();
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
        
        // Deduplicar por ID antes de agregar a la colección
        var tareasUnicas = tareas.GroupBy(t => t.Id).Select(g => g.First()).ToList();
        
        _allTareas.Clear();
        _allTareas.AddRange(tareasUnicas);

        ActualizarListaTareas();

        var pendientes = _allTareas.Where(t => t.Estado != "completada").ToList();
        
        // Ejecutar en MainThread para evitar condiciones de carrera
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            TareasPendientes.Clear();
            foreach(var t in pendientes)
            {
                TareasPendientes.Add(t);
            }
        });
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

        var msgs = mensajesAll.OrderBy(m => m.MarcaTiempo).ToList();
        
        // Marcar primer mensaje de cada día
        DateTime? ultimaFecha = null;
        foreach (var m in msgs)
        {
            m.EsMio = m.De == _currentUser!.Id.ToString();
            m.DeNombre = userMap.TryGetValue(m.De, out var de) ? de : $"ID:{m.De}";
            m.ParaNombre = userMap.TryGetValue(m.Para, out var para) ? para : $"ID:{m.Para}";
            
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

        MainThread.BeginInvokeOnMainThread(() => 
        {
            MensajesNuevosCount = Mensajes.Count(m => !m.Leido && m.Para == _currentUser.Id.ToString());
            HasMensajes = MensajesNuevosCount > 0;
        });
    }

    private async Task CargarEtiquetas()
    {
        var etiquetas = await _databaseService.GetEtiquetasConUsoAsync();
        EtiquetasDisponibles.Clear();
        foreach (var e in etiquetas.Where(et => et.Activa))
        {
            EtiquetasDisponibles.Add(e);
        }
    }

    private void CalcularEstadisticas()
    {
        TotalTareas = _allTareas.Count; 
        TareasCompletadas = _allTareas.Count(t => t.Estado == "completada");
        TareasPendientesCount = _allTareas.Count(t => t.Estado == "pendiente"); 
        HorasRegistradas = 23.5;
        
        // Métricas avanzadas
        CalcularMetricasAvanzadas();
        CargarTareasDelDia();
        CargarHistorialActividad();
        CargarProgresoSemanal();
    }
    
    private void CalcularMetricasAvanzadas()
    {
        var hoy = DateTime.Today;
        var inicioSemana = hoy.AddDays(-(int)hoy.DayOfWeek);
        var finSemana = inicioSemana.AddDays(7);
        
        // Tareas vencidas (pendientes con fecha límite pasada)
        TareasVencidas = _allTareas.Count(t => 
            t.Estado != "completada" && 
            t.FechaVencimiento.Date < hoy);
        
        // Tareas de esta semana
        TareasEstaSemana = _allTareas.Count(t => 
            t.FechaVencimiento.Date >= inicioSemana && 
            t.FechaVencimiento.Date < finSemana);
        
        // Porcentaje de eficiencia (completadas a tiempo vs total completadas)
        var completadas = _allTareas.Where(t => t.Estado == "completada").ToList();
        if (completadas.Any())
        {
            var aTiempo = completadas.Count(t => 
                t.FechaCompletado.HasValue && 
                t.FechaCompletado.Value.Date <= t.FechaVencimiento.Date);
            PorcentajeEficiencia = Math.Round((double)aTiempo / completadas.Count * 100, 1);
        }
        else
        {
            PorcentajeEficiencia = 0;
        }
        
        // Racha de productividad (días consecutivos con al menos 1 tarea completada)
        CalcularRachaProductividad();
    }
    
    private void CalcularRachaProductividad()
    {
        var completadas = _allTareas
            .Where(t => t.Estado == "completada" && t.FechaCompletado.HasValue)
            .OrderByDescending(t => t.FechaCompletado!.Value.Date)
            .ToList();
            
        if (!completadas.Any())
        {
            RachaProductividad = 0;
            RachaTexto = "0 días";
            return;
        }
        
        var fechas = completadas
            .Select(t => t.FechaCompletado!.Value.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
        
        int racha = 0;
        var fechaEsperada = DateTime.Today;
        
        foreach (var fecha in fechas)
        {
            if (fecha == fechaEsperada || fecha == fechaEsperada.AddDays(-1))
            {
                racha++;
                fechaEsperada = fecha.AddDays(-1);
            }
            else
            {
                break;
            }
        }
        
        RachaProductividad = racha;
        RachaTexto = racha == 1 ? "1 día" : $"{racha} días";
    }
    
    private void CargarTareasDelDia()
    {
        var hoy = DateTime.Today;
        
        // Mostrar TODAS las tareas pendientes (no solo urgentes)
        var todasPendientes = _allTareas
            .Where(t => t.Estado != "completada")
            .OrderBy(t => t.FechaVencimiento)
            .ThenByDescending(t => t.Prioridad == "alta" || t.Prioridad == "urgente" || t.Prioridad == "Prioritaria")
            .Take(5)
            .ToList();
        
        // Ejecutar en MainThread para evitar condiciones de carrera
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TareasDelDia.Clear();
            foreach (var t in todasPendientes)
            {
                TareasDelDia.Add(t);
            }
            HayTareasDelDia = TareasDelDia.Any();
        });
    }
    
    private async void CargarHistorialActividad()
    {
        var actividades = new List<ActividadReciente>();
        
        // Tareas completadas recientemente
        var tareasRecientes = _allTareas
            .Where(t => t.Estado == "completada" && t.FechaCompletado.HasValue)
            .OrderByDescending(t => t.FechaCompletado)
            .Take(5)
            .Select(t => new ActividadReciente
            {
                Tipo = "tarea_completada",
                Icono = "check",
                Titulo = $"Completaste \"{t.Titulo}\"",
                Fecha = t.FechaCompletado!.Value,
                ColorIcono = "#10B981"
            });
        actividades.AddRange(tareasRecientes);
        
        // Mensajes enviados recientes
        var mensajesRecientes = Mensajes
            .Where(m => m.EsMio)
            .OrderByDescending(m => m.MarcaTiempo)
            .Take(3)
            .Select(m => new ActividadReciente
            {
                Tipo = "mensaje_enviado",
                Icono = "chat",
                Titulo = $"Enviaste un mensaje",
                Fecha = m.MarcaTiempo,
                ColorIcono = "#3B82F6"
            });
        actividades.AddRange(mensajesRecientes);
        
        // Ordenar por fecha y tomar las últimas 8
        var ordenado = actividades
            .OrderByDescending(a => a.Fecha)
            .Take(8)
            .ToList();
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            HistorialActividad.Clear();
            foreach (var a in ordenado)
            {
                HistorialActividad.Add(a);
            }
        });
    }
    
    private void CargarProgresoSemanal()
    {
        var hoy = DateTime.Today;
        var hace7Dias = hoy.AddDays(-6);
        
        var progreso = new List<ProgresoSemanalItem>();
        
        for (int i = 0; i < 7; i++)
        {
            var fecha = hace7Dias.AddDays(i);
            var completadasEnDia = _allTareas.Count(t => 
                t.Estado == "completada" && 
                t.FechaCompletado.HasValue && 
                t.FechaCompletado.Value.Date == fecha);
            
            progreso.Add(new ProgresoSemanalItem
            {
                Fecha = fecha,
                DiaSemana = ObtenerDiaSemanaCorto(fecha.DayOfWeek),
                Cantidad = completadasEnDia,
                EsHoy = fecha == hoy
            });
        }
        
        MaxTareasSemana = Math.Max(1, progreso.Max(p => p.Cantidad));
        
        // Calcular altura proporcional
        foreach (var p in progreso)
        {
            p.AlturaBarra = MaxTareasSemana > 0 ? (double)p.Cantidad / MaxTareasSemana * 60 : 0;
            p.AlturaBarra = Math.Max(4, p.AlturaBarra); // Mínimo visible
        }
        
        // Ejecutar en MainThread para evitar condiciones de carrera
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ProgresoSemanal.Clear();
            foreach (var p in progreso)
            {
                ProgresoSemanal.Add(p);
            }
        });
    }
    
    private string ObtenerDiaSemanaCorto(DayOfWeek dia)
    {
        return dia switch
        {
            DayOfWeek.Monday => "LU",
            DayOfWeek.Tuesday => "MA",
            DayOfWeek.Wednesday => "MI",
            DayOfWeek.Thursday => "JU",
            DayOfWeek.Friday => "VI",
            DayOfWeek.Saturday => "SA",
            DayOfWeek.Sunday => "DO",
            _ => "??"
        };
    }
    
    [RelayCommand]
    private async Task GuardarNotas()
    {
        if (_currentUser == null) return;
        
        try
        {
            var nota = new NotaRapida
            {
                UsuarioId = _currentUser.Id,
                Contenido = NotasRapidas ?? ""
            };
            await _databaseService.SaveNotaRapidaAsync(nota);
            
            // Mostrar mensaje de confirmación
            NotaGuardadaMensaje = "✓ Nota guardada";
            NotaGuardadaVisible = true;
            
            // Ocultar mensaje después de 3 segundos
            await Task.Delay(3000);
            NotaGuardadaVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando notas: {ex.Message}");
            NotaGuardadaMensaje = "✗ Error al guardar la nota";
            NotaGuardadaVisible = true;
            await Task.Delay(3000);
            NotaGuardadaVisible = false;
        }
    }
    
    [RelayCommand]
    private async Task LimpiarNotas()
    {
        if (_currentUser == null) return;
        
        try
        {
            await _databaseService.LimpiarNotaRapidaAsync(_currentUser.Id);
            NotasRapidas = "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error limpiando notas: {ex.Message}");
        }
    }
    
    private async Task CargarNotasGuardadas()
    {
        if (_currentUser == null) return;
        
        try
        {
            var nota = await _databaseService.GetNotaRapidaPorUsuarioAsync(_currentUser.Id);
            NotasRapidas = nota?.Contenido ?? "";
        }
        catch
        {
            NotasRapidas = "";
        }
    }

    // ============================================
    // COMANDOS DE COMENTARIOS
    // ============================================
    [RelayCommand]
    public async Task AbrirComentarios(Tarea tarea)
    {
        if (tarea == null) return;
        
        TareaSeleccionadaParaComentarios = tarea;
        await CargarComentariosDeTarea();
        await CargarEtiquetasDeTarea();
        MostrarPanelComentarios = true;
    }

    [RelayCommand]
    public void CerrarComentarios()
    {
        MostrarPanelComentarios = false;
        TareaSeleccionadaParaComentarios = null;
        ComentariosTareaActual.Clear();
        EtiquetasTareaActual.Clear();
        NuevoComentarioContenido = "";
    }

    private async Task CargarComentariosDeTarea()
    {
        if (TareaSeleccionadaParaComentarios == null) return;

        var comentarios = await _databaseService.GetComentariosPorTareaAsync(TareaSeleccionadaParaComentarios.Id);
        ComentariosTareaActual.Clear();
        
        foreach (var c in comentarios)
        {
            c.EsMio = c.AutorId == _currentUser?.Id;
            ComentariosTareaActual.Add(c);
        }
        
        ComentariosCount = comentarios.Count;
    }

    private async Task CargarEtiquetasDeTarea()
    {
        if (TareaSeleccionadaParaComentarios == null) return;

        var etiquetasRelaciones = await _databaseService.GetEtiquetasPorTareaAsync(TareaSeleccionadaParaComentarios.Id);
        EtiquetasTareaActual.Clear();
        
        foreach (var etiqueta in EtiquetasDisponibles)
        {
            if (etiquetasRelaciones.Any(te => te.EtiquetaId == etiqueta.Id))
            {
                EtiquetasTareaActual.Add(etiqueta);
            }
        }
    }

    [RelayCommand]
    public async Task AgregarComentario()
    {
        if (string.IsNullOrWhiteSpace(NuevoComentarioContenido) || 
            TareaSeleccionadaParaComentarios == null || 
            _currentUser == null) return;

        var comentario = new Comentario
        {
            TareaId = TareaSeleccionadaParaComentarios.Id,
            AutorId = _currentUser.Id,
            Contenido = NuevoComentarioContenido.Trim(),
            Tipo = "Comentario",
            FechaCreacion = DateTime.Now
        };

        await _databaseService.SaveComentarioAsync(comentario);
        NuevoComentarioContenido = "";
        await CargarComentariosDeTarea();
    }

    [RelayCommand]
    public async Task EliminarComentario(Comentario comentario)
    {
        if (comentario == null || comentario.AutorId != _currentUser?.Id) return;

        await _databaseService.DeleteComentarioAsync(comentario);
        await CargarComentariosDeTarea();
    }

    // ============================================
    // COMANDOS DE ETIQUETAS (FILTRADO)
    // ============================================
    [RelayCommand]
    public async Task FiltrarPorEtiqueta(Etiqueta? etiqueta)
    {
        if (etiqueta == null)
        {
            // Quitar filtro de etiqueta
            EtiquetaFiltroSeleccionada = null;
            FiltrandoPorEtiqueta = false;
            ActualizarListaTareas();
            return;
        }

        EtiquetaFiltroSeleccionada = etiqueta;
        FiltrandoPorEtiqueta = true;

        var tareasConEtiqueta = await _databaseService.FiltrarTareasPorEtiquetasAsync(new List<int> { etiqueta.Id });
        var idsConEtiqueta = tareasConEtiqueta.Select(t => t.Id).ToHashSet();

        Tareas.Clear();
        foreach (var t in _allTareas.Where(tarea => idsConEtiqueta.Contains(tarea.Id)))
        {
            Tareas.Add(t);
        }
    }

    [RelayCommand]
    public void QuitarFiltroEtiqueta()
    {
        EtiquetaFiltroSeleccionada = null;
        FiltrandoPorEtiqueta = false;
        ActualizarListaTareas();
    }

    [RelayCommand]
    private void AplicarFiltro(string filtro)
    {
        FiltroActual = filtro;
        ActualizarListaTareas();
    }

    private void ActualizarListaTareas()
    {
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

        var lista = filtered.ToList();
        
        // Ejecutar en MainThread para evitar condiciones de carrera
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Tareas.Clear();
            foreach (var t in lista)
            {
                Tareas.Add(t);
            }
        });
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
            
            // Generar alerta automática de tarea completada para admins
            await _databaseService.CrearAlertaTareaCompletadaAsync(tarea);
            
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
                Conectado = true;
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
        Conectado = false;
        SessionService.ClearSession();
        Application.Current!.MainPage = Application.Current.Handler!.MauiContext!.Services.GetRequiredService<Views.LoginPage>();
        
        if (Application.Current is App app)
        {
             app.HideWidgetAndShowMain(); 
        }
    }
    
    private async void OnSyncDataChanged(object? sender, string entityType)
    {
        Console.WriteLine($"[CONTADOR] OnSyncDataChanged: {entityType}");
        
        // Ejecutar en MainThread para evitar condiciones de carrera
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Solo recargar todo cuando se completa el FullSync
            if (entityType == "FullSync")
            {
                await CargarTodosLosDatos();
            }
            else if (entityType == "Mensaje") 
            {
                await CargarMensajes();
            }
            else if (entityType == "Comentario" && TareaSeleccionadaParaComentarios != null) 
            {
                await CargarComentariosDeTarea();
            }
            else if (entityType == "Etiqueta" || entityType == "TareaEtiqueta")
            {
                await CargarEtiquetas();
                if (TareaSeleccionadaParaComentarios != null)
                    await CargarEtiquetasDeTarea();
            }
            else if (entityType == "Tarea")
            {
                // Solo recargar tareas cuando es una tarea individual
                await CargarTareas();
                CalcularEstadisticas();
            }
        });
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
