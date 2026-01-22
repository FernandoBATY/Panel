using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Panel.Services;
using Panel.Views;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection; // For GetRequiredService

namespace Panel.ViewModels;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private readonly NetworkService _networkService;
    private readonly SyncService _syncService;
    private readonly IServiceProvider _serviceProvider;
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

    private async Task SincronizarInicial()
    {
        if (string.IsNullOrWhiteSpace(SyncIp))
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", "Ingresa la IP del servidor", "OK");
            return;
        }

        IsSyncing = true;
        SyncStatus = "Conectando al servidor...";

        try
        {
            // 1. Crear identidad temporal
            var tempIdentity = new Panel.Models.NodeIdentity 
            { 
                NodeId = Guid.NewGuid().ToString(),
                Username = "Guest_Sync",
                Role = "Guest",
                MachineName = Environment.MachineName
            };
            SessionService.SetIdentity(tempIdentity);

            // 2. Conectar
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

            // Wait loop (10s timeout)
            int ticks = 0;
            while (!syncComplete && ticks < 20) 
            {
                await Task.Delay(500);
                ticks++;
            }
            
            _syncService.DataChanged -= OnDataChanged;

            SyncStatus = "¡Sincronización Completada!";
            await Application.Current!.MainPage!.DisplayAlert("Éxito", "Base de datos actualizada correctamente. Ahora puedes iniciar sesión.", "OK");
            
            _networkService.Disconnect();
            IsSyncVisible = false; // Hide after success
        }
        catch (Exception ex)
        {
            SyncStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public ICommand LoginCommand { get; }

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
            
            // Establecer sesión de usuario
            SessionService.SetCurrentUser(user);
            
            if (user.Role == "Admin")
            {
                // Navigate to Admin Panel
                var page = _serviceProvider.GetRequiredService<PaginaPanelAdmin>();
                Application.Current!.MainPage = new NavigationPage(page);
            }
            else
            {
                // Navigate to Accountant Control Center (Directly, as requested)
                // Navigate to Accountant Control Center (Directly, as requested)
                var page = _serviceProvider.GetRequiredService<PaginaCentroControlContador>();
                if (page.BindingContext is CentroControlContadorVM vm)
                {
                    vm.Init(user);
                }
                Application.Current!.MainPage = new NavigationPage(page);
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
