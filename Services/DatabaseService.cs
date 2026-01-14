using SQLite;
using Panel.Models;
using System.IO;
using System.Threading.Tasks;

namespace Panel.Services;

public class DatabaseService
{
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

    private async Task Init()
    {
        if (_database is not null)
            return;

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);
        _database = new SQLiteAsyncConnection(databasePath);

        await _database.CreateTableAsync<User>();
        await _database.CreateTableAsync<Tarea>();
        await _database.CreateTableAsync<Documento>();
        await _database.CreateTableAsync<Mensaje>();
        await _database.CreateTableAsync<Alerta>();
        
        // Seed default users
        var users = new List<User>
        {
            new() { Username = "admin", Password = "password", Role = "Admin", Name = "Administrator", Estado = "desconectado", Area = "Admin" },
            new() { Username = "Jazmin", Password = "Jazminjazer1", Role = "Admin", Name = "Jazmin", Estado = "desconectado", Area = "Admin" },
            
            // Ingresos
            new() { Username = "Mari", Password = "Marijazer4", Role = "Contador", Name = "Mari", Estado = "desconectado", Area = "Ingresos" },
            new() { Username = "Karla", Password = "Karlajazer5", Role = "Contador", Name = "Karla", Estado = "desconectado", Area = "Ingresos" },
            new() { Username = "Guadalupe", Password = "Guadalupejazer6", Role = "Contador", Name = "Guadalupe", Estado = "desconectado", Area = "Ingresos" },
            new() { Username = "Jenny", Password = "Jennyjazer7", Role = "Contador", Name = "Jenny", Estado = "desconectado", Area = "Ingresos" },

            // Egresos
            new() { Username = "Perla", Password = "Perlajazer2", Role = "Contador", Name = "Perla", Estado = "desconectado", Area = "Egresos" },
            new() { Username = "Dulce", Password = "Dulcejazer3", Role = "Contador", Name = "Dulce", Estado = "desconectado", Area = "Egresos" },
            new() { Username = "Daniela", Password = "Danielajazer9", Role = "Contador", Name = "Daniela", Estado = "desconectado", Area = "Egresos" },

            // Declaraciones
            new() { Username = "Luis", Password = "Luisjazer10", Role = "Contador", Name = "Luis", Estado = "desconectado", Area = "Declaraciones" },
            new() { Username = "Elio", Password = "Eliojazer8", Role = "Contador", Name = "Elio", Estado = "desconectado", Area = "Declaraciones" }
        };

        foreach (var user in users)
        {
            var existing = await _database.Table<User>()
                                .Where(u => u.Username == user.Username)
                                .FirstOrDefaultAsync();
            if (existing == null)
            {
                await _database.InsertAsync(user);
            }
            else
            {
                // Update role/info if exists (to migrate existing users to new schema format if needed)
                existing.Role = user.Role;
                existing.Area = user.Area; // Update Area
                existing.Name = user.Name; // Ensure Name is populated
                existing.Password = user.Password; // Update Password if changed
                await _database.UpdateAsync(existing);
            }
        }
    }

    public async Task<Panel.Models.User?> LoginAsync(string username, string password)
    {
        await Init();
        return await _database!.Table<Panel.Models.User>()
                            .Where(u => u.Username == username && u.Password == password)
                            .FirstOrDefaultAsync();
    }
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

    public async Task<int> SaveUserAsync(User user, bool skipSync = false)
    {
        await Init();
        int result;
        if (user.Id == 0)
            result = await _database!.InsertAsync(user);
        else
            result = await _database!.UpdateAsync(user);

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
        var result = await _database!.InsertAsync(tarea);
        
        // Notificar sincronización (solo si no viene de red)
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Insert, tarea, "Tarea");
        }
        
        return result;
    }

    public async Task<int> UpdateTareaAsync(Tarea tarea, bool skipSync = false)
    {
        await Init();
        var result = await _database!.UpdateAsync(tarea);
        
        // Notificar sincronización (solo si no viene de red)
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Update, tarea, "Tarea");
        }
        
        return result;
    }

    // Métodos para Alertas
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
        var result = await _database!.InsertAsync(alerta);
        
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

    // Métodos para Mensajes
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
        var result = await _database!.InsertAsync(mensaje);
        
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

    // Métodos para Documentos
    public async Task<List<Documento>> GetDocumentosAsync()
    {
        await Init();
        return await _database!.Table<Documento>().ToListAsync();
    }

    public async Task<List<Documento>> GetDocumentosPorTareaAsync(string tareaId)
    {
        await Init();
        return await _database!.Table<Documento>()
            .Where(d => d.TareaId == tareaId)
            .OrderByDescending(d => d.FechaSubida)
            .ToListAsync();
    }

    public async Task<List<Documento>> GetDocumentosPorUsuarioAsync(int userId)
    {
        await Init();
        return await _database!.Table<Documento>()
            .Where(d => d.SubidoPorId == userId)
            .OrderByDescending(d => d.FechaSubida)
            .ToListAsync();
    }

    public async Task<int> SaveDocumentoAsync(Documento documento, bool skipSync = false)
    {
        await Init();
        var result = await _database!.InsertAsync(documento);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Insert, documento, "Documento");
        }
        
        return result;
    }

    public async Task<int> DeleteDocumentoAsync(Documento documento, bool skipSync = false)
    {
        await Init();
        var result = await _database!.DeleteAsync(documento);
        
        if (!skipSync && _syncService != null)
        {
            await _syncService.OnLocalChange(SyncOperation.Delete, documento, "Documento");
        }
        
        return result;
    }
    public async Task ResetDatabaseAsync()
    {
        await Init();
        // Drop volatile data tables
        await _database!.DropTableAsync<Tarea>();
        await _database!.DropTableAsync<Mensaje>();
        await _database!.DropTableAsync<Alerta>();
        await _database!.DropTableAsync<Documento>();
        
        // Recreate them empty
        await _database!.CreateTableAsync<Tarea>();
        await _database!.CreateTableAsync<Mensaje>();
        await _database!.CreateTableAsync<Alerta>();
        await _database!.CreateTableAsync<Documento>();
    }
}
