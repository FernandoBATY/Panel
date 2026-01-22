using Panel.Models;

namespace Panel.Services;

public class SessionService
{
    // Estado de la sesión actual
    private static NodeIdentity? _currentIdentity;
    private static User? _currentUser;

    // Propiedades de acceso público
    public static NodeIdentity? CurrentIdentity => _currentIdentity;
    public static User? CurrentUser => _currentUser;

    // Gestión de la sesión de usuario
    public static void SetCurrentUser(User user)
    {
        _currentUser = user;
        
        _currentIdentity = new NodeIdentity
        {
            NodeId = $"{DeviceInfo.Current.Name}_{Guid.NewGuid().ToString()[..8]}",
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role,
            MachineName = Environment.MachineName,
            ConnectedAt = DateTime.Now
        };
    }

    public static void SetIdentity(NodeIdentity identity)
    {
        _currentIdentity = identity;
        _currentUser = null; 
    }

    public static void ClearSession()
    {
        _currentUser = null;
        _currentIdentity = null;
    }

    // Validación de roles
    public static bool IsAdmin()
    {
        return _currentUser?.Role == "Admin";
    }

    public static bool IsContador()
    {
        return _currentUser?.Role == "Contador";
    }

    public static bool IsAuthenticated()
    {
        return _currentUser != null;
    }

    // Gestión de identificador único de máquina
    public static string GetOrCreateMachineNodeId()
    {
        var stored = Preferences.Get("MachineNodeId", string.Empty);
        if (!string.IsNullOrEmpty(stored)) return stored;
        
        var newId = Guid.NewGuid().ToString();
        Preferences.Set("MachineNodeId", newId);
        Console.WriteLine($"[SESSION] Created new MachineNodeId: {newId}");
        return newId;
    }
}
