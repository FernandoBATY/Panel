using SQLite;

namespace Panel.Models;

public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "Contador"; // "Admin" or "Contador"
    public string Name { get; set; } = string.Empty;
    public string Estado { get; set; } = "desconectado"; // "conectado", "desconectado"

    // Computed properties for UI (ignored in DB)
    [Ignore]
    public int TotalTareas { get; set; }
    [Ignore]
    public int TareasCompletadas { get; set; }
    [Ignore]
    public int TareasPendientes { get; set; }
}
