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
    private readonly IServiceProvider _serviceProvider;
    private string _username = "";
    private string _password = "";
    private string _errorMessage = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public LoginViewModel(DatabaseService databaseService, IServiceProvider serviceProvider)
    {
        _databaseService = databaseService;
        _serviceProvider = serviceProvider;
        LoginCommand = new Command(async () => await LoginAsync());
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
