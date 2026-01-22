using Microsoft.Maui;
using Microsoft.Maui.Platform;

namespace Panel
{
    public partial class App : Application
    {
        private Window? _mainWindow;
        private Window? _widgetWindow;
        private readonly Views.LoginPage _loginPage;
        
        public static new App? Current => Application.Current as App;

        public App(Views.LoginPage loginPage)
        {
            InitializeComponent();
            _loginPage = loginPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            if (_mainWindow != null) return _mainWindow;

            _mainWindow = new Window(_loginPage) { Title = "JAZER Panel" };
            
            _mainWindow.Created += (s, e) => MaximizarVentana(_mainWindow);
            
            _mainWindow.Deactivated += OnMainWindowDeactivated;
            _mainWindow.Activated += OnMainWindowActivated;

            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainPage))
                {
                    MaximizarVentana(_mainWindow);
                }
            };

            return _mainWindow;
        }

        private void OnMainWindowDeactivated(object? sender, EventArgs e)
        {
            if (Panel.Services.SessionService.IsContador())
            {
                MostrarWidget();
            }
        }

        private void OnMainWindowActivated(object? sender, EventArgs e)
        {
            OcultarWidget();
        }

        public void MostrarWidget()
        {
            if (_widgetWindow == null)
            {
                var widgetPage = new Views.WidgetPage();
                
                if (Application.Current?.Windows[0].Page is NavigationPage nav && 
                    nav.CurrentPage is Views.PaginaCentroControlContador centroPage &&
                    centroPage.BindingContext is ViewModels.CentroControlContadorVM vm)
                {
                    widgetPage.SetViewModel(vm);
                }

                _widgetWindow = new Window(widgetPage)
                {
                    Title = "JAZER Widget",
                    MaximumWidth = 350,
                    MaximumHeight = 400
                };
                
                Application.Current?.OpenWindow(_widgetWindow);
                MaximizarVentana(_widgetWindow); 
            }
        }

        public void OcultarWidget()
        {
            if (_widgetWindow != null)
            {
                Application.Current?.CloseWindow(_widgetWindow);
                _widgetWindow = null;
            }
        }

        public void HideWidgetAndShowMain()
        {
            OcultarWidget();
            
            if (_mainWindow != null)
            {
              
#if WINDOWS
                 var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                 if (platformWindow != null)
                 {
                     platformWindow.Activate(); 
                 }
#endif
                 MaximizarVentana(_mainWindow);
            }
        }

        private void MaximizarVentana(Window window)
        {
            if (window == null) return;

#if WINDOWS
            var platformWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (platformWindow != null)
            {
                var appWindow = platformWindow.GetAppWindow();
                if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    if (window == _widgetWindow)
                    {
                        presenter.IsMaximizable = false;
                        presenter.IsMinimizable = false;
                        presenter.IsResizable = false;
                        presenter.SetBorderAndTitleBar(false, false);
                        presenter.IsAlwaysOnTop = false; 
                        
                        var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
                        var screenWidth = displayArea.WorkArea.Width;
                        var x = screenWidth - 370;
                        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, 20, 350, 400));
                    }
                    else
                    {
                        presenter.IsResizable = true;
                        presenter.IsMinimizable = true;
                        presenter.IsMaximizable = true; 
                        presenter.IsAlwaysOnTop = false;
                        presenter.SetBorderAndTitleBar(true, true);
                        
                        presenter.Maximize();
                    }
                }
            }
#endif
        }
    }
}