using Microsoft.Extensions.Logging;
using Panel.Views;

namespace Panel
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<Services.DatabaseService>();
            builder.Services.AddSingleton<Services.NetworkService>();
            builder.Services.AddSingleton<Services.SyncService>();
            builder.Services.AddTransient<ViewModels.LoginViewModel>();
            builder.Services.AddTransient<ViewModels.WidgetContadorVM>();
            builder.Services.AddTransient<ViewModels.PanelAdminVM>();
            builder.Services.AddTransient<ViewModels.CentroControlContadorVM>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<PaginaWidgetContador>();
            builder.Services.AddTransient<PaginaCentroControlContador>();
            builder.Services.AddTransient<PaginaPanelAdmin>();

            return builder.Build();
        }
    }
}
