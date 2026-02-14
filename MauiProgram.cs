using LLMChat.Services;
using LLMChat.ViewModels;
using LLMChat.Views;
using Microsoft.Extensions.Logging;

namespace LLMChat
{
    //Punto de entrada de la aplicación MAUI
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
                })
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
                    //Eliminar borde nativo de los Entry en Windows
                    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoBorderEntry", (handler, view) =>
                    {
                        if (handler.PlatformView is Microsoft.UI.Xaml.Controls.TextBox tb)
                        {
                            tb.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                            tb.Background = null;
                            tb.Resources["TextBoxBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                            tb.Style = null;
                            tb.Loaded += (s, e) =>
                            {
                                tb.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                                tb.Background = null;
                            };
                        }
                    });

                    //Eliminar borde nativo de los Editor en Windows
                    Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoBorderEditor", (handler, view) =>
                    {
                        if (handler.PlatformView is Microsoft.UI.Xaml.Controls.TextBox tb)
                        {
                            tb.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                            tb.Background = null;
                            tb.Resources["TextBoxBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                            tb.Style = null;
                            tb.Loaded += (s, e) =>
                            {
                                tb.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                                tb.Background = null;
                            };
                        }
                    });

                    //Compactar el Switch nativo
                    Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping("CompactSwitch", (handler, view) =>
                    {
                        if (handler.PlatformView is Microsoft.UI.Xaml.Controls.ToggleSwitch toggle)
                        {
                            toggle.MinWidth = 0;
                            toggle.Padding = new Microsoft.UI.Xaml.Thickness(0);
                            toggle.Header = null;
                            toggle.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right;
                        }
                    });
#endif

#if ANDROID
                    //Quitar línea inferior de Entry en Android
                    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderlineEntry", (handler, view) =>
                    {
                        if (handler.PlatformView is Android.Widget.EditText editText)
                        {
                            editText.Background = null;
                            editText.SetBackgroundColor(Android.Graphics.Color.Transparent);
                        }
                    });

                    //Quitar línea inferior de Editor en Android
                    Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderlineEditor", (handler, view) =>
                    {
                        if (handler.PlatformView is Android.Widget.EditText editText)
                        {
                            editText.Background = null;
                            editText.SetBackgroundColor(Android.Graphics.Color.Transparent);
                        }
                    });
#endif
                });

            //Registrar servicios
            builder.Services.AddSingleton<RabbitMqService>();
            builder.Services.AddSingleton<LlmService>();
            builder.Services.AddSingleton<ThemeService>();

            //Registrar ViewModels
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddSingleton<ChatViewModel>();

            //Registrar páginas
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<SettingsPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
