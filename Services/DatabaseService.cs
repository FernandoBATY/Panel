using SQLite;
using Panel.Models;
using System.IO;
using System.Threading.Tasks;

namespace Panel.Services;

public class DatabaseService
{
    // Configuración de la base de datos
    private SQLiteAsyncConnection? _database;
    private const string DatabaseName = "Panel.db3";
    private SyncService? _syncService;

    public DatabaseService()
    {
    }

    public void SetSyncService(SyncService syncService)
    {
        _syncService = syncService;
    }

    // Inicialización de la base de datos
    private async Task Init()
    {
        if (_database is not null)
            return;

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);
        _database = new SQLiteAsyncConnection(databasePath);

        await _database.CreateTableAsync<User>();
        await _database.CreateTableAsync<Tarea>();
        await _database.CreateTableAsync<Mensaje>();
        await _database.CreateTableAsync<Alerta>();
        await _database.CreateTableAsync<PlantillaTarea>();
        await _database.CreateTableAsync<Comentario>();
        await _database.CreateTableAsync<Etiqueta>();
        await _database.CreateTableAsync<TareaEtiqueta>();
        await _database.CreateTableAsync<NotaRapida>();
        
        var admin = await _database.Table<User>()
                            .Where(u => u.Username == "admin")
                            .FirstOrDefaultAsync();

        if (admin == null)
        {
            var newAdmin = new User 
            { 
                Username = "admin", 
                Name = "Administrator", 
                Role = "Admin", 
                Estado = "desconectado", 
                Area = "Admin",
                Password = BCrypt.Net.BCrypt.HashPassword("password") 
            };
            await _database.InsertAsync(newAdmin);
        }
    }

    // Autenticación de usuarios
    public async Task<Panel.Models.User?> LoginAsync(string username, string password)
    {
        await Init();
        var user = await _database!.Table<Panel.Models.User>()
                            .Where(u => u.Username == username)
                            .FirstOrDefaultAsync();

        if (user != null)
        {
            bool verified = false;
            try 
            {
                verified = BCrypt.Net.BCrypt.Verify(password, user.Password);
            }
            catch 
            {
                if (user.Password == password) 
                {
                    verified = true;
                    user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                    await _database.UpdateAsync(user);
                }
            }

            if (verified) return user;
        }

        return null;
    }

    // Gestión de usuarios
    public async Task<List<User>> GetContadoresAsync()
    {
        await Init();
        return await _database!.Table<User>().Where(u => u.Role == "Contador").ToListAsync();
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        await Init();
        return await _database!.Table<User>().ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        await Init();
        return await _database!.Table<User>().Where(u => u.Id == userId).FirstOrDefaultAsync();
    }

    public async Task<int> SaveUserAsync(User user, bool skipSync = false)
    {
        await Init();
        
        if (!string.IsNullOrEmpty(user.Password) && !user.Password.StartsWith("$2"))
        {
             user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        }

        int result;
        if (user.Id == 0)
            result = await _database!.InsertAsync(user);
        else
            result = await _database!.InsertOrReplaceAsync(user);

        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(user.Id == 0 ? SyncOperation.Insert : SyncOperation.Update, user, "User");
        }
        return result;
    }

    public async Task<int> DeleteUserAsync(User user, bool skipSync = false)
    {
        await Init();
        var result = await _database!.DeleteAsync(user);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Delete, user, "User");
        }
        return result;
    }

    // Gestión de tareas
    public async Task<List<Tarea>> GetTareasAsync()
    {
        await Init();
        return await _database!.Table<Tarea>().ToListAsync();
    }

    public async Task<List<Tarea>> GetTareasPorUsuarioAsync(int userId)
    {
        await Init();
        return await _database!.Table<Tarea>().Where(t => t.AsignadoAId == userId).ToListAsync();
    }

    public async Task<int> SaveTareaAsync(Tarea tarea, bool skipSync = false)
    {
        await Init();
        
        var result = await _database!.InsertOrReplaceAsync(tarea);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Update, tarea, "Tarea");
        }
        
        return result;
    }

    public async Task<int> UpdateTareaAsync(Tarea tarea, bool skipSync = false)
    {
        await Init();
        var result = await _database!.UpdateAsync(tarea);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Update, tarea, "Tarea");
        }
        
        return result;
    }

    // Gestión de alertas
    public async Task<List<Alerta>> GetAlertasPorUsuarioAsync(int userId)
    {
        await Init();
        return await _database!.Table<Alerta>()
            .Where(a => a.DestinatarioId == userId)
            .OrderByDescending(a => a.FechaCreacion)
            .ToListAsync();
    }

    public async Task<int> SaveAlertaAsync(Alerta alerta, bool skipSync = false)
    {
        await Init();
        var result = await _database!.InsertOrReplaceAsync(alerta);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Insert, alerta, "Alerta");
        }
        
        return result;
    }

    public async Task<int> UpdateAlertaAsync(Alerta alerta, bool skipSync = false)
    {
        await Init();
        var result = await _database!.UpdateAsync(alerta);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Update, alerta, "Alerta");
        }
        
        return result;
    }

    // ============================================
    // ALERTAS AUTOMÁTICAS RELACIONADAS CON TAREAS
    // ============================================

    /// <summary>
    /// Genera alertas automáticas para tareas próximas a vencer (2 días antes)
    /// </summary>
    public async Task<int> GenerarAlertasVencimientoAsync()
    {
        await Init();
        var alertasGeneradas = 0;
        var fechaLimite = DateTime.Now.AddDays(2);
        
        var tareasPorVencer = await _database!.Table<Tarea>()
            .Where(t => t.Estado != "completada" && 
                       t.FechaVencimiento <= fechaLimite && 
                       t.FechaVencimiento > DateTime.Now)
            .ToListAsync();

        foreach (var tarea in tareasPorVencer)
        {
            // Verificar si ya existe alerta de vencimiento para esta tarea
            var alertaExistente = await _database.Table<Alerta>()
                .Where(a => a.TareaRelacionadaId == tarea.Id && 
                           a.TipoAlertaTarea == "Vencimiento" &&
                           a.FechaCreacion > DateTime.Now.AddDays(-1))
                .FirstOrDefaultAsync();

            if (alertaExistente == null)
            {
                var diasRestantes = (tarea.FechaVencimiento - DateTime.Now).Days;
                var alerta = new Alerta
                {
                    Titulo = "Tarea próxima a vencer",
                    Mensaje = $"La tarea '{tarea.Titulo}' vence en {diasRestantes} día(s)",
                    Prioridad = diasRestantes <= 1 ? "ALTA" : "MEDIA",
                    Tipo = "ALERTA",
                    TipoAlertaTarea = "Vencimiento",
                    TareaRelacionadaId = tarea.Id,
                    DestinatarioId = tarea.AsignadoAId,
                    AutoGenerada = true,
                    Icono = "campana",
                    ColorHex = "#F59E0B",
                    FechaExpiracion = tarea.FechaVencimiento
                };

                await SaveAlertaAsync(alerta);
                alertasGeneradas++;
            }
        }

        return alertasGeneradas;
    }

    /// <summary>
    /// Genera alertas para tareas retrasadas
    /// </summary>
    public async Task<int> GenerarAlertasRetrasadasAsync()
    {
        await Init();
        var alertasGeneradas = 0;
        
        var tareasRetrasadas = await _database!.Table<Tarea>()
            .Where(t => t.Estado != "completada" && t.FechaVencimiento < DateTime.Now)
            .ToListAsync();

        foreach (var tarea in tareasRetrasadas)
        {
            // Solo crear alerta si no hay una reciente (evitar spam)
            var alertaExistente = await _database.Table<Alerta>()
                .Where(a => a.TareaRelacionadaId == tarea.Id && 
                           a.TipoAlertaTarea == "Retrasada" &&
                           a.FechaCreacion > DateTime.Now.AddHours(-12))
                .FirstOrDefaultAsync();

            if (alertaExistente == null)
            {
                var diasRetraso = (DateTime.Now - tarea.FechaVencimiento).Days;
                var alerta = new Alerta
                {
                    Titulo = "Tarea retrasada",
                    Mensaje = $"'{tarea.Titulo}' está retrasada por {diasRetraso} día(s)",
                    Prioridad = "ALTA",
                    Tipo = "ALERTA",
                    TipoAlertaTarea = "Retrasada",
                    TareaRelacionadaId = tarea.Id,
                    DestinatarioId = tarea.AsignadoAId,
                    AutoGenerada = true,
                    Icono = "advertencia",
                    ColorHex = "#EF4444"
                };

                await SaveAlertaAsync(alerta);
                alertasGeneradas++;

                // También actualizar estado de la tarea
                if (tarea.Estado != "retrasada")
                {
                    tarea.Estado = "retrasada";
                    await UpdateTareaAsync(tarea);
                }
            }
        }

        return alertasGeneradas;
    }

    /// <summary>
    /// Crea alerta cuando se asigna una tarea a un contador
    /// </summary>
    public async Task CrearAlertaAsignacionAsync(Tarea tarea, int asignadoPorId)
    {
        var asignadoPor = await GetUserByIdAsync(asignadoPorId);
        var nombreAsignador = asignadoPor?.Name ?? "Admin";

        var alerta = new Alerta
        {
            Titulo = "Nueva tarea asignada",
            Mensaje = $"{nombreAsignador} te asignó: '{tarea.Titulo}'",
            Prioridad = tarea.Prioridad == "Prioritaria" ? "ALTA" : "MEDIA",
            Tipo = "NOTIFICACION",
            TipoAlertaTarea = "Asignacion",
            TareaRelacionadaId = tarea.Id,
            DestinatarioId = tarea.AsignadoAId,
            AutoGenerada = true,
            CreadoPorId = asignadoPorId,
            Icono = "copiar",
            ColorHex = "#3B82F6"
        };

        await SaveAlertaAsync(alerta);
    }

    /// <summary>
    /// Crea alerta cuando se completa una tarea (para el admin)
    /// </summary>
    public async Task CrearAlertaTareaCompletadaAsync(Tarea tarea)
    {
        var usuario = await GetUserByIdAsync(tarea.AsignadoAId);
        var nombreUsuario = usuario?.Name ?? $"Usuario #{tarea.AsignadoAId}";

        // Notificar a todos los admins
        var admins = await _database!.Table<User>().Where(u => u.Role == "Admin").ToListAsync();
        
        foreach (var admin in admins)
        {
            var alerta = new Alerta
            {
                Titulo = "Tarea completada",
                Mensaje = $"{nombreUsuario} completó '{tarea.Titulo}'",
                Prioridad = "BAJA",
                Tipo = "NOTIFICACION",
                TipoAlertaTarea = "Completada",
                TareaRelacionadaId = tarea.Id,
                DestinatarioId = admin.Id,
                AutoGenerada = true,
                Icono = "trofeo",
                ColorHex = "#10B981"
            };

            await SaveAlertaAsync(alerta);
        }
    }

    /// <summary>
    /// Crea alerta cuando hay un nuevo comentario en una tarea
    /// </summary>
    public async Task CrearAlertaNuevoComentarioAsync(Comentario comentario, Tarea tarea)
    {
        var autor = await GetUserByIdAsync(comentario.AutorId);
        var nombreAutor = autor?.Name ?? $"Usuario #{comentario.AutorId}";

        // Notificar al asignado de la tarea (si no es el autor del comentario)
        if (tarea.AsignadoAId != comentario.AutorId)
        {
            var alerta = new Alerta
            {
                Titulo = "Nuevo comentario",
                Mensaje = $"{nombreAutor} comentó en '{tarea.Titulo}'",
                Prioridad = "BAJA",
                Tipo = "NOTIFICACION",
                TipoAlertaTarea = "Comentario",
                TareaRelacionadaId = tarea.Id,
                DestinatarioId = tarea.AsignadoAId,
                AutoGenerada = true,
                CreadoPorId = comentario.AutorId,
                Icono = "chat",
                ColorHex = "#8B5CF6"
            };

            await SaveAlertaAsync(alerta);
        }
    }

    /// <summary>
    /// Obtiene alertas por tarea relacionada
    /// </summary>
    public async Task<List<Alerta>> GetAlertasPorTareaAsync(string tareaId)
    {
        await Init();
        return await _database!.Table<Alerta>()
            .Where(a => a.TareaRelacionadaId == tareaId)
            .OrderByDescending(a => a.FechaCreacion)
            .ToListAsync();
    }

    /// <summary>
    /// Ejecuta revisión automática de alertas (llamar periódicamente)
    /// </summary>
    public async Task<(int vencimiento, int retrasadas)> EjecutarRevisionAlertasAsync()
    {
        var vencimiento = await GenerarAlertasVencimientoAsync();
        var retrasadas = await GenerarAlertasRetrasadasAsync();
        return (vencimiento, retrasadas);
    }

    // Gestión de mensajes
    public async Task<List<Mensaje>> GetMensajesAsync()
    {
        await Init();
        return await _database!.Table<Mensaje>()
            .OrderByDescending(m => m.MarcaTiempo)
            .ToListAsync();
    }

    public async Task<List<Mensaje>> GetMensajesPorUsuarioAsync(int userId)
    {
        await Init();
        var userIdStr = userId.ToString();
        return await _database!.Table<Mensaje>()
            .Where(m => m.Para == userIdStr || m.Para == "todos" || m.De == userIdStr)
            .OrderByDescending(m => m.MarcaTiempo)
            .ToListAsync();
    }

    public async Task<int> SaveMensajeAsync(Mensaje mensaje, bool skipSync = false)
    {
        await Init();
        var result = await _database!.InsertOrReplaceAsync(mensaje);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Insert, mensaje, "Mensaje");
        }
        
        return result;
    }

    public async Task<int> UpdateMensajeAsync(Mensaje mensaje, bool skipSync = false)
    {
        await Init();
        var result = await _database!.UpdateAsync(mensaje);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Update, mensaje, "Mensaje");
        }
        
        return result;
    }

    // Utilidades de base de datos
    public async Task ResetDatabaseAsync()
    {
        await Init();
        await _database!.DropTableAsync<User>(); 
        await _database!.DropTableAsync<Tarea>();
        await _database!.DropTableAsync<Mensaje>();
        await _database!.DropTableAsync<Alerta>();
        await _database!.DropTableAsync<PlantillaTarea>();
        await _database!.DropTableAsync<Comentario>();
        await _database!.DropTableAsync<Etiqueta>();
        await _database!.DropTableAsync<TareaEtiqueta>();
        
        await _database!.CreateTableAsync<User>();
        await _database!.CreateTableAsync<Tarea>();
        await _database!.CreateTableAsync<Mensaje>();
        await _database!.CreateTableAsync<Alerta>();
        await _database!.CreateTableAsync<PlantillaTarea>();
        await _database!.CreateTableAsync<Comentario>();
        await _database!.CreateTableAsync<Etiqueta>();
        await _database!.CreateTableAsync<TareaEtiqueta>();

        var newAdmin = new User 
        { 
            Username = "admin", 
            Name = "Administrator", 
            Role = "Admin", 
            Estado = "desconectado", 
            Area = "Admin",
            Password = BCrypt.Net.BCrypt.HashPassword("password") 
        };
        await _database.InsertAsync(newAdmin);
    }

    public async Task BackupDatabaseAsync(string destinationPath)
    {
        await Init();
        
        try 
        {
            await _database!.ExecuteAsync($"VACUUM INTO ?", destinationPath);
        }
        catch (Exception)
        {
            var sourcePath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);
            try { await _database!.ExecuteAsync("PRAGMA wal_checkpoint(FULL);"); } catch { }
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    #region Gestión de Plantillas de Tareas

    public async Task<List<PlantillaTarea>> GetPlantillasAsync()
    {
        await Init();
        return await _database!.Table<PlantillaTarea>()
            .Where(p => p.Activa)
            .OrderBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task<List<PlantillaTarea>> GetAllPlantillasAsync()
    {
        await Init();
        return await _database!.Table<PlantillaTarea>()
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();
    }

    public async Task<PlantillaTarea?> GetPlantillaByIdAsync(string id)
    {
        await Init();
        return await _database!.Table<PlantillaTarea>()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PlantillaTarea>> GetPlantillasPorFrecuenciaAsync(string frecuencia)
    {
        await Init();
        return await _database!.Table<PlantillaTarea>()
            .Where(p => p.Frecuencia == frecuencia && p.Activa)
            .ToListAsync();
    }

    public async Task<int> SavePlantillaAsync(PlantillaTarea plantilla, bool skipSync = false)
    {
        await Init();
        
        var existing = await _database!.Table<PlantillaTarea>()
            .Where(p => p.Id == plantilla.Id)
            .FirstOrDefaultAsync();

        int result;
        if (existing == null)
        {
            plantilla.FechaCreacion = DateTime.Now;
            result = await _database!.InsertAsync(plantilla);
        }
        else
        {
            result = await _database!.UpdateAsync(plantilla);
        }

        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(
                existing == null ? SyncOperation.Insert : SyncOperation.Update, 
                plantilla, 
                "PlantillaTarea");
        }
        
        return result;
    }

    public async Task<int> DeletePlantillaAsync(PlantillaTarea plantilla, bool skipSync = false)
    {
        await Init();
        var result = await _database!.DeleteAsync(plantilla);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Delete, plantilla, "PlantillaTarea");
        }
        
        return result;
    }

    public async Task<Tarea> GenerarTareaDesdePlantillaAsync(PlantillaTarea plantilla, int? asignadoAId = null, DateTime? fechaVencimiento = null)
    {
        await Init();
        
        var nuevaTarea = new Tarea
        {
            Id = Guid.NewGuid().ToString(),
            Titulo = $"{plantilla.Nombre} - {DateTime.Now:MMMM yyyy}",
            Descripcion = plantilla.DescripcionBase,
            CategoriaKPI = plantilla.CategoriaKPI,
            Prioridad = plantilla.Prioridad,
            TiempoEstimado = plantilla.TiempoEstimadoHoras,
            FechaAsignacion = DateTime.Now,
            FechaVencimiento = fechaVencimiento ?? DateTime.Now.AddDays(plantilla.DiasAnticipacion > 0 ? plantilla.DiasAnticipacion : 7),
            AsignadoAId = asignadoAId ?? plantilla.AsignadoPorDefectoId,
            Estado = "pendiente"
        };

        await SaveTareaAsync(nuevaTarea);

        // Actualizar última ejecución de la plantilla
        plantilla.UltimaEjecucion = DateTime.Now;
        await SavePlantillaAsync(plantilla);

        return nuevaTarea;
    }

    public async Task<List<Tarea>> GenerarTareasRecurrentesAsync()
    {
        await Init();
        var tareasGeneradas = new List<Tarea>();
        var hoy = DateTime.Now;

        var plantillas = await GetPlantillasAsync();

        foreach (var plantilla in plantillas.Where(p => p.Frecuencia != "Manual"))
        {
            bool debeGenerar = plantilla.Frecuencia switch
            {
                "Diaria" => plantilla.UltimaEjecucion?.Date != hoy.Date,
                "Semanal" => (int)hoy.DayOfWeek == plantilla.DiaEjecucion && 
                             plantilla.UltimaEjecucion?.Date != hoy.Date,
                "Mensual" => hoy.Day == plantilla.DiaEjecucion && 
                             plantilla.UltimaEjecucion?.Month != hoy.Month,
                "Trimestral" => hoy.Day == plantilla.DiaEjecucion && 
                                (hoy.Month == 1 || hoy.Month == 4 || hoy.Month == 7 || hoy.Month == 10) &&
                                plantilla.UltimaEjecucion?.Month != hoy.Month,
                _ => false
            };

            if (debeGenerar)
            {
                var tarea = await GenerarTareaDesdePlantillaAsync(plantilla);
                tareasGeneradas.Add(tarea);
            }
        }

        return tareasGeneradas;
    }

    #endregion

    #region Gestión de Comentarios

    public async Task<List<Comentario>> GetComentariosPorTareaAsync(string tareaId)
    {
        await Init();
        return await _database!.Table<Comentario>()
            .Where(c => c.TareaId == tareaId)
            .OrderBy(c => c.FechaCreacion)
            .ToListAsync();
    }

    public async Task<List<Comentario>> GetComentariosRecientesAsync(int limite = 50)
    {
        await Init();
        return await _database!.Table<Comentario>()
            .OrderByDescending(c => c.FechaCreacion)
            .Take(limite)
            .ToListAsync();
    }

    public async Task<int> GetCantidadComentariosPorTareaAsync(string tareaId)
    {
        await Init();
        return await _database!.Table<Comentario>()
            .Where(c => c.TareaId == tareaId)
            .CountAsync();
    }

    public async Task<int> SaveComentarioAsync(Comentario comentario, bool skipSync = false)
    {
        await Init();
        
        var existing = await _database!.Table<Comentario>()
            .Where(c => c.Id == comentario.Id)
            .FirstOrDefaultAsync();

        int result;
        if (existing == null)
        {
            comentario.FechaCreacion = DateTime.Now;
            result = await _database!.InsertAsync(comentario);
        }
        else
        {
            comentario.Editado = true;
            comentario.FechaEdicion = DateTime.Now;
            result = await _database!.UpdateAsync(comentario);
        }

        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(
                existing == null ? SyncOperation.Insert : SyncOperation.Update, 
                comentario, 
                "Comentario");
        }
        
        return result;
    }

    public async Task<int> DeleteComentarioAsync(Comentario comentario, bool skipSync = false)
    {
        await Init();
        var result = await _database!.DeleteAsync(comentario);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Delete, comentario, "Comentario");
        }
        
        return result;
    }

    public async Task<int> DeleteComentariosPorTareaAsync(string tareaId)
    {
        await Init();
        var comentarios = await GetComentariosPorTareaAsync(tareaId);
        int count = 0;
        foreach (var c in comentarios)
        {
            await DeleteComentarioAsync(c);
            count++;
        }
        return count;
    }

    #endregion

    #region Gestión de Etiquetas

    public async Task<List<Etiqueta>> GetEtiquetasAsync()
    {
        await Init();
        return await _database!.Table<Etiqueta>()
            .Where(e => e.Activa)
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }

    public async Task<List<Etiqueta>> GetAllEtiquetasAsync()
    {
        await Init();
        return await _database!.Table<Etiqueta>()
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }

    public async Task<Etiqueta?> GetEtiquetaByIdAsync(int id)
    {
        await Init();
        return await _database!.Table<Etiqueta>()
            .Where(e => e.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<Etiqueta?> GetEtiquetaByNombreAsync(string nombre)
    {
        await Init();
        return await _database!.Table<Etiqueta>()
            .Where(e => e.Nombre == nombre)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveEtiquetaAsync(Etiqueta etiqueta, bool skipSync = false)
    {
        await Init();
        
        // Verificar si ya existe en la base de datos local
        var existing = await _database!.Table<Etiqueta>()
            .Where(e => e.Id == etiqueta.Id)
            .FirstOrDefaultAsync();
        
        int result;
        if (existing == null)
        {
            if (etiqueta.FechaCreacion == default)
                etiqueta.FechaCreacion = DateTime.Now;
            result = await _database!.InsertAsync(etiqueta);
        }
        else
        {
            result = await _database!.UpdateAsync(etiqueta);
        }

        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(
                existing == null ? SyncOperation.Insert : SyncOperation.Update, 
                etiqueta, 
                "Etiqueta");
        }
        
        return result;
    }

    public async Task<int> DeleteEtiquetaAsync(Etiqueta etiqueta, bool skipSync = false)
    {
        await Init();
        
        // Eliminar todas las relaciones TareaEtiqueta primero
        var relaciones = await _database!.Table<TareaEtiqueta>()
            .Where(te => te.EtiquetaId == etiqueta.Id)
            .ToListAsync();
        
        foreach (var rel in relaciones)
        {
            await _database!.DeleteAsync(rel);
        }
        
        var result = await _database!.DeleteAsync(etiqueta);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Delete, etiqueta, "Etiqueta");
        }
        
        return result;
    }

    public async Task<List<Etiqueta>> GetEtiquetasConUsoAsync()
    {
        await Init();
        var etiquetas = await GetEtiquetasAsync();
        
        foreach (var etiqueta in etiquetas)
        {
            etiqueta.TotalUsos = await _database!.Table<TareaEtiqueta>()
                .Where(te => te.EtiquetaId == etiqueta.Id)
                .CountAsync();
        }
        
        return etiquetas.OrderByDescending(e => e.TotalUsos).ToList();
    }

    #endregion

    #region Gestión de TareaEtiqueta (Relaciones)

    public async Task<List<TareaEtiqueta>> GetEtiquetasPorTareaAsync(string tareaId)
    {
        await Init();
        var relaciones = await _database!.Table<TareaEtiqueta>()
            .Where(te => te.TareaId == tareaId)
            .ToListAsync();

        // Enriquecer con datos de etiqueta
        foreach (var rel in relaciones)
        {
            var etiqueta = await GetEtiquetaByIdAsync(rel.EtiquetaId);
            if (etiqueta != null)
            {
                rel.NombreEtiqueta = etiqueta.Nombre;
                rel.ColorEtiqueta = etiqueta.ColorHex;
                rel.IconoEtiqueta = etiqueta.Icono;
            }
        }

        return relaciones;
    }

    public async Task<List<Tarea>> GetTareasPorEtiquetaAsync(int etiquetaId)
    {
        await Init();
        var relaciones = await _database!.Table<TareaEtiqueta>()
            .Where(te => te.EtiquetaId == etiquetaId)
            .ToListAsync();

        var tareas = new List<Tarea>();
        var todasTareas = await GetTareasAsync();
        
        foreach (var rel in relaciones)
        {
            var tarea = todasTareas.FirstOrDefault(t => t.Id == rel.TareaId);
            if (tarea != null)
            {
                tareas.Add(tarea);
            }
        }

        return tareas;
    }

    public async Task<int> AsignarEtiquetaATareaAsync(string tareaId, int etiquetaId, int asignadoPorId, bool skipSync = false)
    {
        await Init();
        
        // Verificar si ya existe la relación
        var existente = await _database!.Table<TareaEtiqueta>()
            .Where(te => te.TareaId == tareaId && te.EtiquetaId == etiquetaId)
            .FirstOrDefaultAsync();

        if (existente != null)
            return 0; // Ya existe

        var relacion = new TareaEtiqueta
        {
            TareaId = tareaId,
            EtiquetaId = etiquetaId,
            AsignadoPorId = asignadoPorId,
            FechaAsignacion = DateTime.Now
        };

        var result = await _database!.InsertAsync(relacion);

        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Insert, relacion, "TareaEtiqueta");
        }

        return result;
    }

    public async Task<int> RemoverEtiquetaDeTareaAsync(string tareaId, int etiquetaId, bool skipSync = false)
    {
        await Init();
        
        var relacion = await _database!.Table<TareaEtiqueta>()
            .Where(te => te.TareaId == tareaId && te.EtiquetaId == etiquetaId)
            .FirstOrDefaultAsync();

        if (relacion == null)
            return 0;

        var result = await _database!.DeleteAsync(relacion);

        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Delete, relacion, "TareaEtiqueta");
        }

        return result;
    }

    public async Task<int> ActualizarEtiquetasDeTareaAsync(string tareaId, List<int> etiquetaIds, int asignadoPorId)
    {
        await Init();
        
        // Obtener etiquetas actuales
        var actuales = await GetEtiquetasPorTareaAsync(tareaId);
        var actualesIds = actuales.Select(e => e.EtiquetaId).ToList();

        // Remover las que ya no están
        foreach (var actual in actuales)
        {
            if (!etiquetaIds.Contains(actual.EtiquetaId))
            {
                await RemoverEtiquetaDeTareaAsync(tareaId, actual.EtiquetaId);
            }
        }

        // Agregar las nuevas
        foreach (var etiquetaId in etiquetaIds)
        {
            if (!actualesIds.Contains(etiquetaId))
            {
                await AsignarEtiquetaATareaAsync(tareaId, etiquetaId, asignadoPorId);
            }
        }

        return etiquetaIds.Count;
    }

    public async Task<List<Tarea>> FiltrarTareasPorEtiquetasAsync(List<int> etiquetaIds, bool todasLasEtiquetas = false)
    {
        await Init();
        
        var todasTareas = await GetTareasAsync();
        var tareasConEtiquetas = new List<Tarea>();

        foreach (var tarea in todasTareas)
        {
            var etiquetasDeTarea = await GetEtiquetasPorTareaAsync(tarea.Id);
            var etiquetaIdsDeTarea = etiquetasDeTarea.Select(e => e.EtiquetaId).ToList();

            bool coincide = todasLasEtiquetas
                ? etiquetaIds.All(id => etiquetaIdsDeTarea.Contains(id))
                : etiquetaIds.Any(id => etiquetaIdsDeTarea.Contains(id));

            if (coincide)
            {
                tareasConEtiquetas.Add(tarea);
            }
        }

        return tareasConEtiquetas;
    }

    #endregion

    #region Notas Rápidas

    /// <summary>
    /// Obtiene las notas rápidas de un usuario
    /// </summary>
    public async Task<NotaRapida?> GetNotaRapidaPorUsuarioAsync(int usuarioId)
    {
        await Init();
        return await _database!.Table<NotaRapida>()
            .Where(n => n.UsuarioId == usuarioId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Guarda o actualiza la nota rápida de un usuario
    /// </summary>
    public async Task SaveNotaRapidaAsync(NotaRapida nota)
    {
        await Init();
        
        var existente = await _database!.Table<NotaRapida>()
            .Where(n => n.UsuarioId == nota.UsuarioId)
            .FirstOrDefaultAsync();
        
        if (existente != null)
        {
            nota.Id = existente.Id;
            nota.FechaCreacion = existente.FechaCreacion;
            nota.FechaModificacion = DateTime.Now;
            await _database.UpdateAsync(nota);
        }
        else
        {
            nota.FechaCreacion = DateTime.Now;
            nota.FechaModificacion = DateTime.Now;
            await _database.InsertAsync(nota);
        }
    }

    /// <summary>
    /// Limpia/borra la nota rápida de un usuario
    /// </summary>
    public async Task LimpiarNotaRapidaAsync(int usuarioId)
    {
        await Init();
        await _database!.Table<NotaRapida>()
            .Where(n => n.UsuarioId == usuarioId)
            .DeleteAsync();
    }

    #endregion
}
