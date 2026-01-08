namespace Panel
{
    public partial class App : Application
    {
        private readonly Views.LoginPage _loginPage;

        public App(Views.LoginPage loginPage)
        {
            InitializeComponent();
            _loginPage = loginPage;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_loginPage);
        }
    }
}