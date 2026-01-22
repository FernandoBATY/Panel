using SQLite;

namespace Panel.Models;

public class User
{
    // Identificación y credenciales
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    // Perfil y estado
    public string Role { get; set; } = "Contador"; 
    public string Name { get; set; } = string.Empty;
    public string Estado { get; set; } = "desconectado"; 
    public string Area { get; set; } = "General"; 
    public string FotoPerfil { get; set; } = string.Empty;

    // Métricas calculadas (no persistentes)
    [Ignore]
    public int TotalTareas { get; set; }
    [Ignore]
    public int TareasCompletadas { get; set; }
    [Ignore]
    public int TareasPendientes { get; set; }
    [Ignore]
    public string SessionDuration { get; set; } = "00:00:00";
}
