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
        await _database.CreateTableAsync<Documento>();
        await _database.CreateTableAsync<Mensaje>();
        await _database.CreateTableAsync<Alerta>();
        
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

    // Gestión de documentos
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

    // Utilidades de base de datos
    public async Task ResetDatabaseAsync()
    {
        await Init();
        await _database!.DropTableAsync<User>(); 
        await _database!.DropTableAsync<Tarea>();
        await _database!.DropTableAsync<Mensaje>();
        await _database!.DropTableAsync<Alerta>();
        await _database!.DropTableAsync<Documento>();
        
        await _database!.CreateTableAsync<User>();
        await _database!.CreateTableAsync<Tarea>();
        await _database!.CreateTableAsync<Mensaje>();
        await _database!.CreateTableAsync<Alerta>();
        await _database!.CreateTableAsync<Documento>();

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
}
