using LLMChat.ViewModels;

namespace LLMChat.Views
{
    //Página de ajustes
    public partial class SettingsPage : ContentPage
    {
        private readonly SettingsViewModel vm;

        public SettingsPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            vm = viewModel;
            viewModel.LoadSavedSettings();

#if WINDOWS
            //Deshabilitar scroll wheel en el Picker de modelos
            ModelPicker.HandlerChanged += (s, e) =>
            {
                if (ModelPicker.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ComboBox combo)
                {
                    combo.AddHandler(
                        Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent,
                        new Microsoft.UI.Xaml.Input.PointerEventHandler((sender, args) =>
                            args.Handled = true),
                        true);
                }
            };
#endif
        }

        //Cargar modelos al aparecer la página si no hay ninguno
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (vm.AvailableModels.Count == 0)
                await vm.LoadModelsCommand.ExecuteAsync(null);
        }
    }
}
