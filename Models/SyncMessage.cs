namespace Panel.Models;

public class SyncMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public NodeIdentity? Sender { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public SyncOperation Operation { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityJson { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
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
