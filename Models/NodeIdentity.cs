namespace Panel.Models;

public class NodeIdentity
{
    // Identificación de nodo y usuario
    public string NodeId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    
    // Información de dispositivo y conexión
    public string MachineName { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; } = DateTime.Now;
    public string IpAddress { get; set; } = string.Empty;
    public string SessionDuration { get; set; } = "00:00:00";
}
