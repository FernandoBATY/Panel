using Microsoft.UI.Xaml;


namespace Panel.WinUI
{
    public partial class App : MauiWinUIApplication
    {
  
        public App()
        {
            System.AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as System.Exception;
                string path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "Panel_CrashLog.txt");
                string msg = $"[{System.DateTime.Now}] CRASH:\n{ex?.ToString() ?? "Unknown Error"}\n\n";
                System.IO.File.AppendAllText(path, msg);
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
