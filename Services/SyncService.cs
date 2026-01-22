using System.Text.Json;
using Panel.Models;

namespace Panel.Services;

public class SyncService
{
    // Servicios dependientes
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;

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

            Console.WriteLine($"[SYNC] Mensaje recibido de {message.Sender?.Username}: {message.Operation} {message.EntityType}");

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

            DataChanged?.Invoke(this, message.EntityType);
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
                         LogSync($"✅ Usuario guardado: {user.Username}");
                     }
                     else
                     {
                         LogSync($"⚠️ Usuario {user.Username} ya existe, omitiendo.");
                     }
                }
                else
                {
                    LogSync("❌ User deserializado es NULL!");
                }
                break;

            case "Tarea":
                var tarea = JsonSerializer.Deserialize<Tarea>(message.EntityJson);
                if (tarea != null)
                {
                    var existing = await _databaseService.GetTareasAsync();
                    if (!existing.Any(t => t.Id == tarea.Id))
                    {
                        await _databaseService.SaveTareaAsync(tarea, skipSync: true);
                        Console.WriteLine($"[SYNC] Tarea insertada: {tarea.Titulo}");
                    }
                }
                break;

            case "Mensaje":
                var msg = JsonSerializer.Deserialize<Mensaje>(message.EntityJson);
                if (msg != null)
                {
                    var existing = await _databaseService.GetMensajesAsync();
                    if (!existing.Any(m => m.Id == msg.Id))
                    {
                        await _databaseService.SaveMensajeAsync(msg, skipSync: true);
                        Console.WriteLine($"[SYNC] Mensaje insertado de {msg.De}");
                    }
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
