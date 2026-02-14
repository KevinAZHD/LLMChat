using LLMChat.Services;

namespace LLMChat
{
    public partial class App : Application
    {
        //Constructor con inyección del servicio de tema
        public App(ThemeService servicioTema)
        {
            InitializeComponent();
            servicioTema.ApplyTheme();
        }

        //Crear ventana principal
        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new AppShell());
    }
}