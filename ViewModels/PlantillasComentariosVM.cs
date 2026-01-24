using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Panel.Models;
using Panel.Services;
using System.Collections.ObjectModel;

namespace Panel.ViewModels;

// Usamos IconoEtiqueta del PanelAdminVM (ya está definido allí)

public partial class PlantillasComentariosVM : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly SyncService _syncService;

    #region Propiedades Observable

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    // Usuario actual
    [ObservableProperty]
    private User? _usuarioActual;

    #endregion

    #region Colecciones

    public ObservableCollection<PlantillaTarea> Plantillas { get; } = new();
    public ObservableCollection<Comentario> ComentariosTarea { get; } = new();
    public ObservableCollection<Etiqueta> Etiquetas { get; } = new();
    public ObservableCollection<Etiqueta> EtiquetasSeleccionadas { get; } = new();
    public ObservableCollection<TareaEtiqueta> EtiquetasDeTarea { get; } = new();
    public ObservableCollection<User> Usuarios { get; } = new();

    #endregion

    #region Propiedades para Plantillas

    [ObservableProperty]
    private string _plantillaNombre = string.Empty;

    [ObservableProperty]
    private string _plantillaDescripcion = string.Empty;

    [ObservableProperty]
    private string _plantillaCategoriaKPI = "General";

    [ObservableProperty]
    private decimal _plantillaTiempoEstimado = 4;

    [ObservableProperty]
    private string _plantillaPrioridad = "Media";

    [ObservableProperty]
    private string _plantillaFrecuencia = "Manual";

    [ObservableProperty]
    private int _plantillaDiaEjecucion = 1;

    [ObservableProperty]
    private int _plantillaDiasAnticipacion = 7;

    [ObservableProperty]
    private string _plantillaChecklist = string.Empty;

    [ObservableProperty]
    private User? _plantillaAsignadoPorDefecto;

    [ObservableProperty]
    private PlantillaTarea? _plantillaSeleccionada;

    [ObservableProperty]
    private bool _isEditandoPlantilla;

    public List<string> Frecuencias { get; } = new() { "Manual", "Diaria", "Semanal", "Mensual", "Trimestral" };
    public List<string> Prioridades { get; } = new() { "Baja", "Media", "Alta", "Urgente" };
    public List<string> CategoriasKPI { get; } = new() { "General", "Ingresos", "Egresos", "Declaraciones", "OpinionSAT", "EnvioPrevios" };

    #endregion

    #region Propiedades para Comentarios

    [ObservableProperty]
    private string _tareaIdSeleccionada = string.Empty;

    [ObservableProperty]
    private string _nuevoComentarioContenido = string.Empty;

    [ObservableProperty]
    private Comentario? _comentarioEditando;

    [ObservableProperty]
    private int _totalComentarios;

    #endregion

    #region Propiedades para Etiquetas

    [ObservableProperty]
    private string _etiquetaNombre = string.Empty;

    [ObservableProperty]
    private string _etiquetaColorHex = "#3B82F6";

    [ObservableProperty]
    private string _etiquetaDescripcion = string.Empty;

    [ObservableProperty]
    private string _etiquetaIcono = "tag";

    [ObservableProperty]
    private Etiqueta? _etiquetaSeleccionada;

    [ObservableProperty]
    private bool _isEditandoEtiqueta;

    public List<string> ColoresDisponibles { get; } = new()
    {
        "#EF4444", // Rojo
        "#F59E0B", // Naranja
        "#10B981", // Verde
        "#3B82F6", // Azul
        "#8B5CF6", // Morado
        "#EC4899", // Rosa
        "#6B7280", // Gris
        "#14B8A6", // Teal
        "#F97316", // Naranja oscuro
        "#84CC16"  // Lima
    };

    public List<IconoEtiqueta> IconosDisponibles { get; } = new()
    {
        new("tag", "M20.59 13.41l-7.17 7.17a2 2 0 01-2.83 0L2 12V2h10l8.59 8.59a2 2 0 010 2.82zM7 7h.01"),
        new("campana", "M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9 M13.73 21a2 2 0 0 1-3.46 0"),
        new("dinero", "M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"),
        new("nube", "M18 10h-1.26A8 8 0 1 0 9 20h9a5 5 0 0 0 0-10z"),
        new("advertencia", "M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0zM12 9v4M12 17h.01"),
        new("trofeo", "M6 9H4.5a2.5 2.5 0 0 1 0-5H6 M18 9h1.5a2.5 2.5 0 0 0 0-5H18 M4 22h16 M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22 M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22 M18 2H6v7a6 6 0 0 0 12 0V2z")
    };

    #endregion

    #region Constructor

    public PlantillasComentariosVM(DatabaseService databaseService, SyncService syncService)
    {
        _databaseService = databaseService;
        _syncService = syncService;

        UsuarioActual = SessionService.CurrentUser;
        _syncService.DataChanged += OnSyncDataChanged;

        Task.Run(CargarDatosIniciales);
    }

    #endregion

    #region Métodos de Carga

    [RelayCommand]
    public async Task CargarDatosIniciales()
    {
        IsBusy = true;

        await CargarPlantillas();
        await CargarEtiquetas();
        await CargarUsuarios();

        IsBusy = false;
    }

    private async Task CargarPlantillas()
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

    private async Task CargarEtiquetas()
    {
        var etiquetas = await _databaseService.GetEtiquetasConUsoAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Etiquetas.Clear();
            foreach (var e in etiquetas)
            {
                Etiquetas.Add(e);
            }
        });
    }

    private async Task CargarUsuarios()
    {
        var usuarios = await _databaseService.GetContadoresAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Usuarios.Clear();
            foreach (var u in usuarios)
            {
                Usuarios.Add(u);
            }
        });
    }

    [RelayCommand]
    public async Task CargarComentariosDeTarea(string tareaId)
    {
        if (string.IsNullOrEmpty(tareaId)) return;

        TareaIdSeleccionada = tareaId;
        var comentarios = await _databaseService.GetComentariosPorTareaAsync(tareaId);
        var usuarios = await _databaseService.GetAllUsersAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ComentariosTarea.Clear();
            foreach (var c in comentarios)
            {
                var autor = usuarios.FirstOrDefault(u => u.Id == c.AutorId);
                c.AutorNombre = autor?.Name ?? "Usuario";
                c.AutorFoto = autor?.FotoPerfil ?? "";
                c.EsMio = c.AutorId == UsuarioActual?.Id;

                ComentariosTarea.Add(c);
            }
            TotalComentarios = comentarios.Count;
        });
    }

    [RelayCommand]
    public async Task CargarEtiquetasDeTarea(string tareaId)
    {
        if (string.IsNullOrEmpty(tareaId)) return;

        var etiquetas = await _databaseService.GetEtiquetasPorTareaAsync(tareaId);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            EtiquetasDeTarea.Clear();
            foreach (var te in etiquetas)
            {
                EtiquetasDeTarea.Add(te);
            }

            // Marcar etiquetas seleccionadas
            foreach (var e in Etiquetas)
            {
                e.EstaSeleccionada = etiquetas.Any(te => te.EtiquetaId == e.Id);
            }
        });
    }

    #endregion

    #region Comandos de Plantillas

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
        plantilla.CreadorId = UsuarioActual?.Id ?? 0;

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

        var usuario = Usuarios.FirstOrDefault(u => u.Id == plantilla.AsignadoPorDefectoId);
        PlantillaAsignadoPorDefecto = usuario;
    }

    [RelayCommand]
    public async Task EliminarPlantilla(PlantillaTarea plantilla)
    {
        if (plantilla == null) return;

        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Confirmar",
            $"¿Eliminar la plantilla '{plantilla.Nombre}'?",
            "Sí, eliminar",
            "Cancelar");

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
    }

    [RelayCommand]
    public async Task GenerarTareasRecurrentes()
    {
        IsBusy = true;

        var tareasGeneradas = await _databaseService.GenerarTareasRecurrentesAsync();

        IsBusy = false;

        if (tareasGeneradas.Count > 0)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Tareas Generadas",
                $"Se generaron {tareasGeneradas.Count} tareas automáticamente.",
                "OK");
        }
        else
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Sin Tareas",
                "No hay tareas recurrentes para generar hoy.",
                "OK");
        }
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

    #region Comandos de Comentarios

    [RelayCommand]
    public async Task AgregarComentario()
    {
        if (string.IsNullOrWhiteSpace(NuevoComentarioContenido) || string.IsNullOrEmpty(TareaIdSeleccionada))
            return;

        var comentario = new Comentario
        {
            TareaId = TareaIdSeleccionada,
            AutorId = UsuarioActual?.Id ?? 0,
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
        await CargarComentariosDeTarea(TareaIdSeleccionada);
    }

    [RelayCommand]
    public async Task EditarComentario(Comentario comentario)
    {
        if (comentario == null || comentario.AutorId != UsuarioActual?.Id) return;

        string? nuevoContenido = await Application.Current!.Windows[0].Page!.DisplayPromptAsync(
            "Editar Comentario",
            "Modifica tu comentario:",
            initialValue: comentario.Contenido,
            maxLength: 2000);

        if (!string.IsNullOrWhiteSpace(nuevoContenido))
        {
            comentario.Contenido = nuevoContenido;
            await _databaseService.SaveComentarioAsync(comentario);
            await CargarComentariosDeTarea(TareaIdSeleccionada);
        }
    }

    [RelayCommand]
    public async Task EliminarComentario(Comentario comentario)
    {
        if (comentario == null) return;

        // Solo el autor o admin puede eliminar
        if (comentario.AutorId != UsuarioActual?.Id && UsuarioActual?.Role != "Admin")
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Error",
                "Solo puedes eliminar tus propios comentarios.",
                "OK");
            return;
        }

        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Confirmar",
            "¿Eliminar este comentario?",
            "Sí",
            "No");

        if (confirm)
        {
            await _databaseService.DeleteComentarioAsync(comentario);
            await CargarComentariosDeTarea(TareaIdSeleccionada);
        }
    }

    // Método de utilidad para agregar comentarios del sistema
    public async Task AgregarComentarioSistema(string tareaId, string mensaje)
    {
        var comentario = new Comentario
        {
            TareaId = tareaId,
            AutorId = 0, // Sistema
            Contenido = mensaje,
            Tipo = "Sistema"
        };

        await _databaseService.SaveComentarioAsync(comentario);
    }

    #endregion

    #region Comandos de Etiquetas

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
        etiqueta.CreadorId = UsuarioActual?.Id ?? 0;

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
            "Confirmar",
            $"¿Eliminar la etiqueta '{etiqueta.Nombre}'?\n\nEsto la quitará de todas las tareas que la usen.",
            "Sí, eliminar",
            "Cancelar");

        if (!confirm) return;

        await _databaseService.DeleteEtiquetaAsync(etiqueta);
        await CargarEtiquetas();
    }

    [RelayCommand]
    public void CancelarEdicionEtiqueta()
    {
        LimpiarFormularioEtiqueta();
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
    public async Task AsignarEtiquetaATarea(Etiqueta etiqueta)
    {
        if (etiqueta == null || string.IsNullOrEmpty(TareaIdSeleccionada)) return;

        if (etiqueta.EstaSeleccionada)
        {
            await _databaseService.RemoverEtiquetaDeTareaAsync(TareaIdSeleccionada, etiqueta.Id);
        }
        else
        {
            await _databaseService.AsignarEtiquetaATareaAsync(TareaIdSeleccionada, etiqueta.Id, UsuarioActual?.Id ?? 0);
        }

        await CargarEtiquetasDeTarea(TareaIdSeleccionada);
        await CargarEtiquetas(); // Para actualizar contadores
    }

    [RelayCommand]
    public async Task ActualizarEtiquetasDeTarea(List<int> etiquetaIds)
    {
        if (string.IsNullOrEmpty(TareaIdSeleccionada)) return;

        await _databaseService.ActualizarEtiquetasDeTareaAsync(
            TareaIdSeleccionada,
            etiquetaIds,
            UsuarioActual?.Id ?? 0);

        await CargarEtiquetasDeTarea(TareaIdSeleccionada);
        await CargarEtiquetas();
    }

    public void ToggleEtiquetaSeleccion(Etiqueta etiqueta)
    {
        if (etiqueta == null) return;

        if (EtiquetasSeleccionadas.Contains(etiqueta))
        {
            EtiquetasSeleccionadas.Remove(etiqueta);
            etiqueta.EstaSeleccionada = false;
        }
        else
        {
            EtiquetasSeleccionadas.Add(etiqueta);
            etiqueta.EstaSeleccionada = true;
        }
    }

    #endregion

    #region Navegación

    [RelayCommand]
    private void SetTab(string indexStr)
    {
        if (int.TryParse(indexStr, out int index))
        {
            SelectedTabIndex = index;
        }
    }

    #endregion

    #region Eventos de Sincronización

    private async void OnSyncDataChanged(object? sender, string entityType)
    {
        if (entityType == "PlantillaTarea")
        {
            await CargarPlantillas();
        }
        else if (entityType == "Comentario")
        {
            if (!string.IsNullOrEmpty(TareaIdSeleccionada))
            {
                await CargarComentariosDeTarea(TareaIdSeleccionada);
            }
        }
        else if (entityType == "Etiqueta" || entityType == "TareaEtiqueta")
        {
            await CargarEtiquetas();
            if (!string.IsNullOrEmpty(TareaIdSeleccionada))
            {
                await CargarEtiquetasDeTarea(TareaIdSeleccionada);
            }
        }
    }

    #endregion
}
