namespace Panel.Models;

public class SyncMessage
{
    // Metadatos del mensaje
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public NodeIdentity? Sender { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Datos de sincronización
    public SyncOperation Operation { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityJson { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    
    // Datos de archivo (para fotos de perfil)
    public string? FileData { get; set; } // Base64 de la imagen
    public string? FileName { get; set; } // Nombre del archivo
    public int UserId { get; set; } // ID del usuario dueño de la foto
}

public enum SyncOperation
{
    Insert,
    Update,
    Delete,
    FullSync,
    Hello,
    Heartbeat
}
