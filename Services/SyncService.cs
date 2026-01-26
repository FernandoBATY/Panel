using System.Text.Json;
using Panel.Models;

namespace Panel.Services;

public class SyncService
{
    // Servicios dependientes
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;
    
    // Flag para indicar si estamos recibiendo una sincronización completa
    private bool _isReceivingFullSync = false;
    
    // Control de mensajes procesados recientemente para evitar duplicados
    private readonly HashSet<string> _processedMessages = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    // Evento para notificar cambios de datos
    public event EventHandler<string>? DataChanged; 

    public SyncService(DatabaseService databaseService, NetworkService networkService)
    {
        _databaseService = databaseService;
        _networkService = networkService;

        _networkService.MessageReceived += OnMessageReceived;
        _networkService.ClientConnected += OnClientConnected;
    }

    // Sincronización completa al conectar nuevo cliente
    private async void OnClientConnected(object? sender, NodeIdentity clientIdentity)
    {
        if (!SessionService.IsAdmin()) return; 

        LogSync($"=== SERVIDOR: Enviando FullSync a {clientIdentity.Username} ===");

        try
        {
     
            var users = await _databaseService.GetAllUsersAsync();
            LogSync($"SERVIDOR: Total usuarios en DB: {users.Count}");
            foreach(var u in users) 
            {
                LogSync($"SERVIDOR: Enviando usuario: {u.Username} (Id: {u.Id})");
                await SendDirectSync(u, "User", clientIdentity.NodeId);
            }

      
            var tareas = await _databaseService.GetTareasAsync();
            LogSync($"SERVIDOR: Total tareas en DB: {tareas.Count}");
            foreach(var t in tareas) await SendDirectSync(t, "Tarea", clientIdentity.NodeId);

            var msgs = await _databaseService.GetMensajesAsync();
            LogSync($"SERVIDOR: Total mensajes en DB: {msgs.Count}");
            foreach(var m in msgs) await SendDirectSync(m, "Mensaje", clientIdentity.NodeId);

            // Sincronizar Plantillas de Tareas
            var plantillas = await _databaseService.GetAllPlantillasAsync();
            LogSync($"SERVIDOR: Total plantillas en DB: {plantillas.Count}");
            foreach(var p in plantillas) await SendDirectSync(p, "PlantillaTarea", clientIdentity.NodeId);

            // Sincronizar Comentarios
            var comentarios = await _databaseService.GetComentariosRecientesAsync(500);
            LogSync($"SERVIDOR: Total comentarios en DB: {comentarios.Count}");
            foreach(var c in comentarios) await SendDirectSync(c, "Comentario", clientIdentity.NodeId);

            // Sincronizar Etiquetas
            var etiquetas = await _databaseService.GetAllEtiquetasAsync();
            LogSync($"SERVIDOR: Total etiquetas en DB: {etiquetas.Count}");
            foreach(var e in etiquetas) await SendDirectSync(e, "Etiqueta", clientIdentity.NodeId);

            // Sincronizar TareaEtiqueta (relaciones)
            var tareasAll = await _databaseService.GetTareasAsync();
            foreach(var t in tareasAll)
            {
                var relaciones = await _databaseService.GetEtiquetasPorTareaAsync(t.Id);
                foreach(var te in relaciones) await SendDirectSync(te, "TareaEtiqueta", clientIdentity.NodeId);
            }
            
            var doneMsg = new SyncMessage
            {
                Sender = SessionService.CurrentIdentity,
                Operation = SyncOperation.FullSync,
                EntityType = "Done",
                Timestamp = DateTime.UtcNow
            };
            await _networkService.SendDirectlyToNodeAsync(clientIdentity.NodeId, doneMsg);
            LogSync("SERVIDOR: Enviado marcador 'Done'");
        }
        catch (Exception ex)
        {
            LogSync($"SERVIDOR ERROR: {ex.Message}");
        }
    }

    // Envío directo de entidad a un cliente específico
    private async Task SendDirectSync(object entity, string type, string targetNodeId)
    {
         var msg = new SyncMessage
        {
            Sender = SessionService.CurrentIdentity,
            Operation = SyncOperation.Insert, 
            EntityType = type,
            EntityJson = JsonSerializer.Serialize(entity),
            Timestamp = DateTime.UtcNow
        };
        await _networkService.SendDirectlyToNodeAsync(targetNodeId, msg);
    }

    #region Local Changes (Outgoing)

    public async Task OnLocalChange(SyncOperation operation, object entity, string entityType)
    {
        var message = new SyncMessage
        {
            Sender = SessionService.CurrentIdentity,
            Operation = operation,
            EntityType = entityType,
            EntityJson = JsonSerializer.Serialize(entity),
            Timestamp = DateTime.UtcNow
        };

        if (SessionService.IsAdmin())
        {
            await _networkService.BroadcastMessageAsync(message);
        }
        else
        {
            await _networkService.SendMessageAsync(message);
        }

        Console.WriteLine($"[SYNC] Cambio local enviado: {operation} {entityType}");
    }

    #endregion

    #region Remote Changes (Incoming)

    private async void OnMessageReceived(object? sender, SyncMessage message)
    {
        try
        {
            if (message.Sender?.NodeId == SessionService.CurrentIdentity?.NodeId)
                return;

            // Limpiar mensajes procesados antiguos cada 5 minutos
            if ((DateTime.UtcNow - _lastCleanup).TotalMinutes > 5)
            {
                _processedMessages.Clear();
                _lastCleanup = DateTime.UtcNow;
            }

            // Crear un identificador único para este mensaje
            var messageId = $"{message.EntityType}_{message.Operation}_{message.EntityJson?.GetHashCode()}_{message.Timestamp:yyyyMMddHHmmss}";
            
            // Si ya procesamos este mensaje, ignorarlo
            if (_processedMessages.Contains(messageId))
            {
                Console.WriteLine($"[SYNC] Mensaje duplicado ignorado: {messageId}");
                return;
            }
            
            // Marcar mensaje como procesado
            _processedMessages.Add(messageId);

            Console.WriteLine($"[SYNC] Mensaje recibido de {message.Sender?.Username}: {message.Operation} {message.EntityType}");

            // Detectar inicio de sincronización completa (primer User que llega del Admin)
            if (message.Sender?.Role == "Admin" && message.Operation == SyncOperation.Insert && message.EntityType == "User" && !_isReceivingFullSync)
            {
                _isReceivingFullSync = true;
                Console.WriteLine("[SYNC] Iniciando recepción de FullSync...");
            }

            switch (message.Operation)
            {
                case SyncOperation.Insert:
                    await ApplyInsert(message);
                    break;
                case SyncOperation.Update:
                    await ApplyUpdate(message);
                    break;
                case SyncOperation.Delete:
                    await ApplyDelete(message);
                    break;
                case SyncOperation.FullSync:
                    await ApplyFullSync(message);
                    break;
            }

            // Solo notificar cambios si NO es un mensaje de FullSync individual
            // Los mensajes de FullSync (Done) dispararán la recarga completa
            if (message.EntityType == "Done")
            {
                _isReceivingFullSync = false;
                Console.WriteLine("[SYNC] FullSync completado, notificando cambios...");
                DataChanged?.Invoke(this, "FullSync");
            }
            else if (!_isReceivingFullSync)
            {
                DataChanged?.Invoke(this, message.EntityType);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SYNC] Error procesando mensaje: {ex.Message}");
        }
    }

    // Registro de eventos de sincronización
    private void LogSync(string message)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SYNC_LOG.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
        Console.WriteLine($"[SYNC] {message}");
    }

    // Aplicar operaciones de inserción
    private async Task ApplyInsert(SyncMessage message)
    {
        LogSync($"ApplyInsert called for EntityType: {message.EntityType}");
        
        switch (message.EntityType)
        {
            case "User":
                LogSync($"User JSON: {message.EntityJson}");
                var user = JsonSerializer.Deserialize<User>(message.EntityJson);
                LogSync($"Deserialized user: {user?.Username ?? "NULL"}");
                
                if (user != null)
                {
                     var existing = await _databaseService.GetAllUsersAsync();
                     LogSync($"Existing users count: {existing.Count}");
                     LogSync($"Checking if user.Id {user.Id} already exists...");
                     
                     if (!existing.Any(u => u.Id == user.Id))
                     {
                         LogSync($"User {user.Username} doesn't exist, saving...");
                         await _databaseService.SaveUserAsync(user, skipSync: true);
                         LogSync($"[OK] Usuario guardado: {user.Username}");
                     }
                     else
                     {
                         LogSync($"[WARN] Usuario {user.Username} ya existe, omitiendo.");
                     }
                }
                else
                {
                    LogSync("[ERROR] User deserializado es NULL!");
                }
                break;

            case "Tarea":
                var tarea = JsonSerializer.Deserialize<Tarea>(message.EntityJson);
                if (tarea != null)
                {
                    // Verificar si ya existe la tarea antes de insertar
                    var tareasExistentes = await _databaseService.GetTareasAsync();
                    var tareaExistente = tareasExistentes.FirstOrDefault(t => t.Id == tarea.Id);
                    
                    if (tareaExistente == null)
                    {
                        await _databaseService.SaveTareaAsync(tarea, skipSync: true);
                        Console.WriteLine($"[SYNC] Tarea sincronizada: {tarea.Titulo}");
                    }
                    else
                    {
                        Console.WriteLine($"[SYNC] Tarea {tarea.Titulo} ya existe, omitiendo inserción");
                    }
                }
                break;

            case "Mensaje":
                var msg = JsonSerializer.Deserialize<Mensaje>(message.EntityJson);
                if (msg != null)
                {
                    // InsertOrReplaceAsync evita duplicados por PrimaryKey
                    await _databaseService.SaveMensajeAsync(msg, skipSync: true);
                    Console.WriteLine($"[SYNC] Mensaje sincronizado de {msg.De}");
                }
                break;

            case "PlantillaTarea":
                var plantilla = JsonSerializer.Deserialize<PlantillaTarea>(message.EntityJson);
                if (plantilla != null)
                {
                    // SavePlantillaAsync ya maneja InsertOrUpdate
                    await _databaseService.SavePlantillaAsync(plantilla, skipSync: true);
                    Console.WriteLine($"[SYNC] Plantilla sincronizada: {plantilla.Nombre}");
                }
                break;

            case "Comentario":
                var comentario = JsonSerializer.Deserialize<Comentario>(message.EntityJson);
                if (comentario != null)
                {
                    // SaveComentarioAsync ya verifica si existe
                    await _databaseService.SaveComentarioAsync(comentario, skipSync: true);
                    Console.WriteLine($"[SYNC] Comentario sincronizado en tarea {comentario.TareaId}");
                }
                break;

            case "Etiqueta":
                var etiqueta = JsonSerializer.Deserialize<Etiqueta>(message.EntityJson);
                if (etiqueta != null)
                {
                    // SaveEtiquetaAsync ya maneja InsertOrUpdate
                    await _databaseService.SaveEtiquetaAsync(etiqueta, skipSync: true);
                    Console.WriteLine($"[SYNC] Etiqueta sincronizada: {etiqueta.Nombre}");
                }
                break;

            case "TareaEtiqueta":
                var tareaEtiqueta = JsonSerializer.Deserialize<TareaEtiqueta>(message.EntityJson);
                if (tareaEtiqueta != null)
                {
                    await _databaseService.AsignarEtiquetaATareaAsync(
                        tareaEtiqueta.TareaId, 
                        tareaEtiqueta.EtiquetaId, 
                        tareaEtiqueta.AsignadoPorId, 
                        skipSync: true);
                    Console.WriteLine($"[SYNC] TareaEtiqueta insertada");
                }
                break;
                
            case "Done":
                Console.WriteLine("[SYNC] Received Done marker - sync complete");
                break;
        }
    }

    // Aplicar operaciones de actualización
    private async Task ApplyUpdate(SyncMessage message)
    {
        switch (message.EntityType)
        {
            case "Tarea":
                var tarea = JsonSerializer.Deserialize<Tarea>(message.EntityJson);
                if (tarea != null)
                {
                    var localTareas = await _databaseService.GetTareasAsync();
                    var localTarea = localTareas.FirstOrDefault(t => t.Id == tarea.Id);

                    if (localTarea == null)
                    {
                        await _databaseService.SaveTareaAsync(tarea, skipSync: true);
                    }
                    else
                    {
                        await _databaseService.UpdateTareaAsync(tarea, skipSync: true);
                        Console.WriteLine($"[SYNC] Tarea actualizada: {tarea.Titulo}");
                    }
                }
                break;

            case "Mensaje":
                var msgUpdate = JsonSerializer.Deserialize<Mensaje>(message.EntityJson);
                if (msgUpdate != null)
                {
                    await _databaseService.UpdateMensajeAsync(msgUpdate, skipSync: true);
                    Console.WriteLine($"[SYNC] Mensaje actualizado");
                }
                break;

            case "PlantillaTarea":
                var plantillaUpdate = JsonSerializer.Deserialize<PlantillaTarea>(message.EntityJson);
                if (plantillaUpdate != null)
                {
                    await _databaseService.SavePlantillaAsync(plantillaUpdate, skipSync: true);
                    Console.WriteLine($"[SYNC] Plantilla actualizada: {plantillaUpdate.Nombre}");
                }
                break;

            case "Comentario":
                var comentarioUpdate = JsonSerializer.Deserialize<Comentario>(message.EntityJson);
                if (comentarioUpdate != null)
                {
                    await _databaseService.SaveComentarioAsync(comentarioUpdate, skipSync: true);
                    Console.WriteLine($"[SYNC] Comentario actualizado");
                }
                break;

            case "Etiqueta":
                var etiquetaUpdate = JsonSerializer.Deserialize<Etiqueta>(message.EntityJson);
                if (etiquetaUpdate != null)
                {
                    await _databaseService.SaveEtiquetaAsync(etiquetaUpdate, skipSync: true);
                    Console.WriteLine($"[SYNC] Etiqueta actualizada: {etiquetaUpdate.Nombre}");
                }
                break;
        }
    }

    // Aplicar operaciones de eliminación
    private async Task ApplyDelete(SyncMessage message)
    {
        if (message.Sender?.Role != "Admin")
        {
            Console.WriteLine("[SYNC] Delete rechazado: solo Admin puede eliminar");
            return;
        }

    }

    // Aplicar sincronización completa
    private async Task ApplyFullSync(SyncMessage message)
    {
        Console.WriteLine("[SYNC] Recibiendo sincronización completa...");
        
    }

    #endregion
}
