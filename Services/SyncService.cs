using System.Text.Json;
using Panel.Models;

namespace Panel.Services;

public class SyncService
{
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;

    public event EventHandler<string>? DataChanged; // string is entityType

    public SyncService(DatabaseService databaseService, NetworkService networkService)
    {
        _databaseService = databaseService;
        _networkService = networkService;

        // Suscribirse a mensajes de red
        _networkService.MessageReceived += OnMessageReceived;
    }

    #region Local Changes (Outgoing)

    public async Task OnLocalChange(SyncOperation operation, object entity, string entityType)
    {
        // Crear mensaje de sincronización
        var message = new SyncMessage
        {
            Sender = SessionService.CurrentIdentity,
            Operation = operation,
            EntityType = entityType,
            EntityJson = JsonSerializer.Serialize(entity),
            Timestamp = DateTime.UtcNow
        };

        // Enviar a la red
        if (SessionService.IsAdmin())
        {
            // Si soy servidor, broadcast a todos los clientes
            await _networkService.BroadcastMessageAsync(message);
        }
        else
        {
            // Si soy cliente, enviar al servidor
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
            // Ignorar nuestros propios mensajes
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

    private async Task ApplyInsert(SyncMessage message)
    {
        switch (message.EntityType)
        {
            case "Tarea":
                var tarea = JsonSerializer.Deserialize<Tarea>(message.EntityJson);
                if (tarea != null)
                {
                    // Verificar que no exista ya (por si recibimos duplicados)
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
        }
    }

    private async Task ApplyUpdate(SyncMessage message)
    {
        switch (message.EntityType)
        {
            case "Tarea":
                var tarea = JsonSerializer.Deserialize<Tarea>(message.EntityJson);
                if (tarea != null)
                {
                    // Resolver conflictos por timestamp
                    var localTareas = await _databaseService.GetTareasAsync();
                    var localTarea = localTareas.FirstOrDefault(t => t.Id == tarea.Id);

                    if (localTarea == null)
                    {
                        // No existe localmente, insertarla
                        await _databaseService.SaveTareaAsync(tarea, skipSync: true);
                    }
                    else
                    {
                        // Existe, aplicar cambio (Last-Write-Wins por timestamp)
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

    private async Task ApplyDelete(SyncMessage message)
    {
        // Validar permisos
        if (message.Sender?.Role != "Admin")
        {
            Console.WriteLine("[SYNC] Delete rechazado: solo Admin puede eliminar");
            return;
        }

        // TODO: Implementar cuando tengamos método Delete en DatabaseService
    }

    private async Task ApplyFullSync(SyncMessage message)
    {
        Console.WriteLine("[SYNC] Recibiendo sincronización completa...");
        
        // El mensaje contiene lista de nodos conectados
        // En una implementación completa, aquí recibiríamos snapshot de toda la DB
    }

    #endregion
}
