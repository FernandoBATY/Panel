using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Panel.Services;
using Panel.Views;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection; 

namespace Panel.ViewModels;

public class LoginViewModel : INotifyPropertyChanged
{
    // Servicios dependientes e inyección
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;
    private readonly SyncService _syncService;
    private readonly IServiceProvider _serviceProvider;

    // Estado de entrada y mensajes
    private string _username = "";
    private string _password = "";
    private string _errorMessage = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public LoginViewModel(DatabaseService databaseService, NetworkService networkService, SyncService syncService, IServiceProvider serviceProvider)
    {
        _databaseService = databaseService;
        _networkService = networkService;
        _syncService = syncService;
        _serviceProvider = serviceProvider;
        // Comandos de UI
        LoginCommand = new Command(async () => await LoginAsync());
        SincronizarInicialCommand = new Command(async () => await SincronizarInicial());
        ToggleSyncCommand = new Command(() => IsSyncVisible = !IsSyncVisible);
    }

    public string Username
    {
        get => _username;
        set
        {
            if (_username != value)
            {
                _username = value;
                OnPropertyChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password != value)
            {
                _password = value;
                OnPropertyChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand SincronizarInicialCommand { get; }
    public ICommand ToggleSyncCommand { get; }

    private bool _isSyncVisible;
    public bool IsSyncVisible
    {
        get => _isSyncVisible;
        set
        {
            if (_isSyncVisible != value)
            {
                _isSyncVisible = value;
                OnPropertyChanged();
            }
        }
    }

    private string _syncIp = "";
    public string SyncIp
    {
        get => _syncIp;
        set
        {
            if (_syncIp != value)
            {
                _syncIp = value;
                OnPropertyChanged();
            }
        }
    }

    private string _syncStatus = "";
    public string SyncStatus
    {
        get => _syncStatus;
        set
        {
            if (_syncStatus != value)
            {
                _syncStatus = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isSyncing;
    public bool IsSyncing
    {
        get => _isSyncing;
        set
        {
            if (_isSyncing != value)
            {
                _isSyncing = value;
                OnPropertyChanged();
            }
        }
    }

    // Sincronización inicial para traer datos desde el servidor
    private async Task SincronizarInicial()
    {
        if (string.IsNullOrWhiteSpace(SyncIp))
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert("Error", "Ingresa la IP del servidor", "OK");
            return;
        }

        IsSyncing = true;
        SyncStatus = "Conectando al servidor...";

        try
        {
            var tempIdentity = new Panel.Models.NodeIdentity 
            { 
                NodeId = SessionService.GetOrCreateMachineNodeId(), 
                Username = "Guest_Sync",
                Role = "Guest",
                MachineName = Environment.MachineName
            };
            SessionService.SetIdentity(tempIdentity);

            var connected = await _networkService.ConnectToServerAsync(SyncIp.Trim());
            if (!connected)
            {
                SyncStatus = "Error de conexión";
                await Application.Current!.MainPage!.DisplayAlert("Error", "No se pudo conectar al servidor", "OK");
                IsSyncing = false;
                return;
            }

            SyncStatus = "Recibiendo datos...";
            
            bool syncComplete = false;
            
            void OnDataChanged(object? s, string type)
            {
                if (type == "Done") syncComplete = true;
            }
            
            _syncService.DataChanged += OnDataChanged;

            int ticks = 0;
            while (!syncComplete && ticks < 20) 
            {
                await Task.Delay(500);
                ticks++;
            }
            
            _syncService.DataChanged -= OnDataChanged;

            SyncStatus = "Procesando datos recibidos...";

            await Task.Delay(5000); 

            var users = await _databaseService.GetAllUsersAsync();
            Console.WriteLine($"[SYNC VERIFY] Total users after sync: {users.Count}");
            
            foreach (var u in users)
            {
                Console.WriteLine($"[SYNC VERIFY] - {u.Username} ({u.Role})");
            }

            if (users.Count < 2)
            {
                SyncStatus = "[WARN] No se recibieron usuarios del servidor";
                await Application.Current!.MainPage!.DisplayAlert("Error de Sincronización", 
                    $"Solo se detectaron {users.Count} usuario(s). Verifica que:\n\n" +
                    "1. El servidor esté activo en la otra PC\n" +
                    "2. La IP sea correcta\n" +
                    "3. No haya firewall bloqueando el puerto 5000", "OK");
                _networkService.Disconnect();
                IsSyncing = false;
                return;
            }

            SyncStatus = "¡Sincronización Completada!";
            await Application.Current!.MainPage!.DisplayAlert("Éxito", $"Se sincronizaron {users.Count} usuarios correctamente.\n\nYa puedes iniciar sesión con cualquiera de ellos.", "OK");
            
            _networkService.Disconnect();
            IsSyncVisible = false; // Hide after success
        }
        catch (Exception ex)
        {
            SyncStatus = $"Error: {ex.Message}";
            Console.WriteLine($"[SYNC ERROR] {ex}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public ICommand LoginCommand { get; }

    // Flujo de autenticación y navegación
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Por favor ingrese usuario y contraseña.";
            return;
        }

        var user = await _databaseService.LoginAsync(Username, Password);
        if (user != null)
        {
            ErrorMessage = "";
            
            SessionService.SetCurrentUser(user);
            
            if (user.Role == "Admin")
            {
                try 
                {
                    var page = _serviceProvider.GetRequiredService<PaginaPanelAdmin>();
                    Application.Current!.MainPage = new NavigationPage(page);
                }
                catch (Exception ex)
                {
                   string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOGIN_CRASH.txt");
                   File.WriteAllText(logPath, $"ERROR NAVIGATING TO ADMIN:\n{ex.ToString()}");
                   ErrorMessage = $"Error crítico: {ex.Message}";
                   await Application.Current!.MainPage!.DisplayAlert("Crash Detectado", $"Error guardado en: {logPath}", "OK");
                }
            }
            else
            {
                try
                {
                    var page = _serviceProvider.GetRequiredService<PaginaCentroControlContador>();
                    if (page.BindingContext is CentroControlContadorVM vm)
                    {
                        vm.Init(user);
                    }
                    Application.Current!.MainPage = new NavigationPage(page);
                }
                catch (Exception ex)
                {
                   string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOGIN_CRASH.txt");
                   File.WriteAllText(logPath, $"ERROR NAVIGATING TO ACCOUNTANT:\n{ex.ToString()}");
                   ErrorMessage = $"Error crítico: {ex.Message}";
                   await Application.Current!.MainPage!.DisplayAlert("Crash Detectado", $"Error guardado en: {logPath}", "OK");
                }
            }
        }
        else
        {
            ErrorMessage = "Usuario o contraseña incorrectos.";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
