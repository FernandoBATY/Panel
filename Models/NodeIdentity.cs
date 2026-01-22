namespace Panel.Models;

public class NodeIdentity
{
    public string NodeId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; } = DateTime.Now;
    public string IpAddress { get; set; } = string.Empty;
    public string SessionDuration { get; set; } = "00:00:00";
}
