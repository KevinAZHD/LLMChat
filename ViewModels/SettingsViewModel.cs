using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMChat.Services;
using System.Collections.ObjectModel;

namespace LLMChat.ViewModels
{
    //ViewModel de la página de ajustes
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly LlmService servicioLlm;
        private readonly ThemeService servicioTema;
        private bool ajustesCargados;
        private bool inicializando;

        //Identificación
        [ObservableProperty] private string _userName = "Usuario";

        //Broker
        [ObservableProperty] private string _brokerIp = "localhost";
        [ObservableProperty] private string _brokerPort = "5672";
        [ObservableProperty] private string _exchangeName = "llmchat_exchange";
        [ObservableProperty] private string _rabbitUser = "guest";
        [ObservableProperty] private string _rabbitPassword = "guest";
        [ObservableProperty] private string _rabbitVHost = "/";

        //LLM
        [ObservableProperty] private string _llmUrl = "http://localhost:1234";
        [ObservableProperty] private string _systemPrompt = "Eres un conversador amigable. Responde siempre en español, de forma natural y breve (máximo 2 frases). No menciones que eres una IA ni nada técnico. No repitas lo que ya se ha dicho.";
        [ObservableProperty] private string _model = "";
        [ObservableProperty] private string _temperature = "0.7";
        [ObservableProperty] private string _maxTokens = "150";
        public ObservableCollection<string> AvailableModels { get; } = new();
        [ObservableProperty] private bool _isLoadingModels;

        //Estado
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isStatusVisible;

        //Errores de validación
        [ObservableProperty] private string _userNameError = string.Empty;
        [ObservableProperty] private string _brokerIpError = string.Empty;
        [ObservableProperty] private string _brokerPortError = string.Empty;
        [ObservableProperty] private string _exchangeNameError = string.Empty;
        [ObservableProperty] private string _llmUrlError = string.Empty;
        [ObservableProperty] private string _temperatureError = string.Empty;
        [ObservableProperty] private string _maxTokensError = string.Empty;
        [ObservableProperty] private string _systemPromptError = string.Empty;

        //Apariencia
        [ObservableProperty] private bool _isDarkMode = true;

        //RGB mensaje enviado (0-255)
        [ObservableProperty] private double _sentR = 108;
        [ObservableProperty] private double _sentG = 92;
        [ObservableProperty] private double _sentB = 231;

        //RGB mensaje recibido (0-255)
        [ObservableProperty] private double _recR = 42;
        [ObservableProperty] private double _recG = 42;
        [ObservableProperty] private double _recB = 74;

        //Preview de colores
        [ObservableProperty] private string _sentBubbleHex = ThemeService.DefaultSentBubble;
        [ObservableProperty] private Color _sentPreviewColor = Color.FromArgb(ThemeService.DefaultSentBubble);
        [ObservableProperty] private string _receivedBubbleHex = ThemeService.DefaultReceivedBubble;
        [ObservableProperty] private Color _receivedPreviewColor = Color.FromArgb(ThemeService.DefaultReceivedBubble);

        //Texto adaptativo por contraste
        [ObservableProperty] private Color _sentTextColor = Colors.White;
        [ObservableProperty] private Color _receivedTextColor = Colors.White;

        //Constructor
        public SettingsViewModel(LlmService servicioLlm, ThemeService servicioTema)
        {
            this.servicioLlm = servicioLlm;
            this.servicioTema = servicioTema;

            // Configurar IPs por defecto para Android Emulator
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                BrokerIp = "10.0.2.2";
                LlmUrl = "http://10.0.2.2:1234";
            }

            LoadSavedSettings();
        }



        //Al cambiar modo oscuro, guardar y aplicar
        partial void OnIsDarkModeChanged(bool value)
        {
            if (inicializando) return;
            Preferences.Set("IsDarkMode", value);
            servicioTema.ApplyTheme();
        }

        //Handlers de cambio RGB
        partial void OnSentRChanged(double value) => ActualizarPreviewEnviado();
        partial void OnSentGChanged(double value) => ActualizarPreviewEnviado();
        partial void OnSentBChanged(double value) => ActualizarPreviewEnviado();
        partial void OnRecRChanged(double value) => ActualizarPreviewRecibido();
        partial void OnRecGChanged(double value) => ActualizarPreviewRecibido();
        partial void OnRecBChanged(double value) => ActualizarPreviewRecibido();

        //Actualizar preview del color enviado
        private void ActualizarPreviewEnviado()
        {
            var r = (byte)Math.Clamp((int)SentR, 0, 255);
            var g = (byte)Math.Clamp((int)SentG, 0, 255);
            var b = (byte)Math.Clamp((int)SentB, 0, 255);
            SentPreviewColor = Color.FromRgb(r, g, b);
            SentBubbleHex = $"#{r:X2}{g:X2}{b:X2}";
            SentTextColor = ThemeService.ContrastText(SentBubbleHex);
        }

        //Actualizar preview del color recibido
        private void ActualizarPreviewRecibido()
        {
            var r = (byte)Math.Clamp((int)RecR, 0, 255);
            var g = (byte)Math.Clamp((int)RecG, 0, 255);
            var b = (byte)Math.Clamp((int)RecB, 0, 255);
            ReceivedPreviewColor = Color.FromRgb(r, g, b);
            ReceivedBubbleHex = $"#{r:X2}{g:X2}{b:X2}";
            ReceivedTextColor = ThemeService.ContrastText(ReceivedBubbleHex);
        }

        //Cargar modelos disponibles del servidor LLM
        [RelayCommand]
        private async Task LoadModelsAsync()
        {
            IsLoadingModels = true;
            StatusMessage = "Buscando modelos...";
            IsStatusVisible = true;

            try
            {
                servicioLlm.UrlBase = LlmUrl;
                var modelos = await servicioLlm.GetAvailableModelsAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableModels.Clear();
                    foreach (var m in modelos) AvailableModels.Add(m);

                    if (modelos.Count > 0)
                    {
                        if (string.IsNullOrEmpty(Model) || !modelos.Contains(Model))
                            Model = modelos[0];
                        StatusMessage = $"{modelos.Count} modelo(s) encontrado(s)";
                    }
                    else
                    {
                        StatusMessage = "No se encontraron modelos";
                    }
                    OcultarEstadoConRetardo();
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                OcultarEstadoConRetardo();
            }
            finally { IsLoadingModels = false; }
        }

        //Restaurar colores de burbujas a los valores por defecto
        [RelayCommand]
        private void ResetBubbleColors()
        {
            HexARgb(ThemeService.DefaultSentBubble, out var sr, out var sg, out var sb);
            SentR = sr; SentG = sg; SentB = sb;
            HexARgb(ThemeService.DefaultReceivedBubble, out var rr, out var rg, out var rb);
            RecR = rr; RecG = rg; RecB = rb;
        }

        //Guardar toda la configuración
        [RelayCommand]
        private void Save()
        {
            if (!ValidarTodo())
            {
                StatusMessage = "Corrige los errores antes de guardar";
                IsStatusVisible = true;
                OcultarEstadoConRetardo(5000);
                return;
            }

            //Configuración general
            Preferences.Set("UserName", UserName);
            Preferences.Set("BrokerIp", BrokerIp);
            Preferences.Set("BrokerPort", BrokerPort);
            Preferences.Set("ExchangeName", ExchangeName);
            Preferences.Set("RabbitUser", RabbitUser);
            Preferences.Set("RabbitPassword", RabbitPassword);
            Preferences.Set("RabbitVHost", RabbitVHost);
            Preferences.Set("LlmUrl", LlmUrl);
            Preferences.Set("SystemPrompt", SystemPrompt);
            Preferences.Set("Model", Model);
            Preferences.Set("Temperature", Temperature);
            Preferences.Set("MaxTokens", MaxTokens);

            //Apariencia
            Preferences.Set("SentBubbleColor", SentBubbleHex);
            Preferences.Set("ReceivedBubbleColor", ReceivedBubbleHex);
            Preferences.Set("IsDarkMode", IsDarkMode);
            servicioTema.ApplyTheme();

            StatusMessage = "Configuración guardada";
            IsStatusVisible = true;
            OcultarEstadoConRetardo();
        }

        //Validar todos los campos
        private bool ValidarTodo()
        {
            bool ok = true;

            if (string.IsNullOrWhiteSpace(UserName))
            { UserNameError = "El nombre no puede estar vacío"; ok = false; }
            else if (UserName.Length > 50)
            { UserNameError = "Máximo 50 caracteres"; ok = false; }
            else UserNameError = string.Empty;

            if (string.IsNullOrWhiteSpace(BrokerIp))
            { BrokerIpError = "La IP no puede estar vacía"; ok = false; }
            else if (BrokerIp.Contains(' '))
            { BrokerIpError = "No puede contener espacios"; ok = false; }
            else BrokerIpError = string.Empty;

            if (string.IsNullOrWhiteSpace(BrokerPort))
            { BrokerPortError = "El puerto no puede estar vacío"; ok = false; }
            else if (!int.TryParse(BrokerPort, out var puerto) || puerto < 1 || puerto > 65535)
            { BrokerPortError = "Puerto inválido (1-65535)"; ok = false; }
            else BrokerPortError = string.Empty;

            if (string.IsNullOrWhiteSpace(ExchangeName))
            { ExchangeNameError = "No puede estar vacío"; ok = false; }
            else if (ExchangeName.Contains(' '))
            { ExchangeNameError = "No puede contener espacios"; ok = false; }
            else ExchangeNameError = string.Empty;

            if (string.IsNullOrWhiteSpace(LlmUrl))
            { LlmUrlError = "La URL no puede estar vacía"; ok = false; }
            else if (!Uri.TryCreate(LlmUrl, UriKind.Absolute, out var uri)
                     || (uri.Scheme != "http" && uri.Scheme != "https"))
            { LlmUrlError = "URL inválida (ej: http://localhost:1234)"; ok = false; }
            else LlmUrlError = string.Empty;

            if (string.IsNullOrWhiteSpace(SystemPrompt))
            { SystemPromptError = "No puede estar vacío"; ok = false; }
            else SystemPromptError = string.Empty;

            if (string.IsNullOrWhiteSpace(Temperature))
            { TemperatureError = "Requerido"; ok = false; }
            else if (!double.TryParse(Temperature,
                         System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture,
                         out var temp) || temp < 0.0 || temp > 2.0)
            { TemperatureError = "Valor entre 0.0 y 2.0"; ok = false; }
            else TemperatureError = string.Empty;

            if (string.IsNullOrWhiteSpace(MaxTokens))
            { MaxTokensError = "Requerido"; ok = false; }
            else if (!int.TryParse(MaxTokens, out var tokens) || tokens < 1 || tokens > 8192)
            { MaxTokensError = "Valor entre 1 y 8192"; ok = false; }
            else MaxTokensError = string.Empty;

            return ok;
        }

        //Ocultar mensaje de estado después de un retardo
        private async void OcultarEstadoConRetardo(int ms = 3000)
        {
            await Task.Delay(ms);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsStatusVisible = false;
                StatusMessage = string.Empty;
            });
        }

        //Convertir hex a valores RGB (0-255)
        private static void HexARgb(string hex, out double r, out double g, out double b)
        {
            r = g = b = 0;
            try
            {
                var c = Color.FromArgb(hex);
                r = Math.Round(c.Red * 255);
                g = Math.Round(c.Green * 255);
                b = Math.Round(c.Blue * 255);
            }
            catch { }
        }

        //Cargar ajustes guardados en Preferences
        public void LoadSavedSettings()
        {
            if (ajustesCargados) return;
            ajustesCargados = true;
            inicializando = true;

            UserName = Preferences.Get("UserName", UserName);
            BrokerIp = Preferences.Get("BrokerIp", BrokerIp);
            BrokerPort = Preferences.Get("BrokerPort", BrokerPort);
            ExchangeName = Preferences.Get("ExchangeName", ExchangeName);
            RabbitUser = Preferences.Get("RabbitUser", RabbitUser);
            RabbitPassword = Preferences.Get("RabbitPassword", RabbitPassword);
            RabbitVHost = Preferences.Get("RabbitVHost", RabbitVHost);
            LlmUrl = Preferences.Get("LlmUrl", LlmUrl);
            SystemPrompt = Preferences.Get("SystemPrompt", SystemPrompt);
            Model = Preferences.Get("Model", Model);
            Temperature = Preferences.Get("Temperature", Temperature);
            MaxTokens = Preferences.Get("MaxTokens", MaxTokens);

            IsDarkMode = Preferences.Get("IsDarkMode", true);

            //Cargar colores de burbujas
            var hexEnviado = Preferences.Get("SentBubbleColor", ThemeService.DefaultSentBubble);
            HexARgb(hexEnviado, out var sr, out var sg, out var sb);
            SentR = sr; SentG = sg; SentB = sb;

            var hexRecibido = Preferences.Get("ReceivedBubbleColor", ThemeService.DefaultReceivedBubble);
            HexARgb(hexRecibido, out var rr, out var rg, out var rb);
            RecR = rr; RecG = rg; RecB = rb;

            inicializando = false;
        }
    }
}
