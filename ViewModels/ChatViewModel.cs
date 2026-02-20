using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMChat.Models;
using LLMChat.Services;
using System.Collections.ObjectModel;

namespace LLMChat.ViewModels
{
    //ViewModel principal del chat
    public partial class ChatViewModel : ObservableObject
    {
        private readonly RabbitMqService servicioRabbit;
        private readonly LlmService servicioLlm;
        private readonly SettingsViewModel ajustesVm;
        private bool procesando;

        //Propiedades observables
        [ObservableProperty] private string _messageText = string.Empty;
        [ObservableProperty] private string _statusText = "Desconectado";
        [ObservableProperty] private bool _isConnected;
        [ObservableProperty] private bool _autoModeEnabled = true;
        [ObservableProperty] private bool _isOtherTyping;
        [ObservableProperty] private string _otherTypingText = string.Empty;
        [ObservableProperty] private bool _isAutoConversing;

        //Colección de mensajes
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        //Constructor con inyección de dependencias
        public ChatViewModel(RabbitMqService servicioRabbit, LlmService servicioLlm, SettingsViewModel ajustesVm)
        {
            this.servicioRabbit = servicioRabbit;
            this.servicioLlm = servicioLlm;
            this.ajustesVm = ajustesVm;

            servicioRabbit.OnMessageReceived += AlRecibirMensaje;
            servicioRabbit.OnTypingReceived += AlRecibirEscritura;
        }

        //Callback cuando el otro usuario está escribiendo
        private void AlRecibirEscritura(string remitente, bool escribiendo)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsOtherTyping = escribiendo;
                OtherTypingText = escribiendo ? $"{remitente} está escribiendo..." : string.Empty;
            });
        }

        //Conectar al broker y al LLM
        [RelayCommand]
        private async Task ConnectAsync()
        {
            try
            {
                servicioRabbit.BrokerHost = ajustesVm.BrokerIp;
                servicioRabbit.BrokerPort = int.TryParse(ajustesVm.BrokerPort, out var puerto) ? puerto : 5672;
                servicioRabbit.ExchangeName = ajustesVm.ExchangeName;
                servicioRabbit.RabbitUser = ajustesVm.RabbitUser;
                servicioRabbit.RabbitPassword = ajustesVm.RabbitPassword;
                servicioRabbit.VirtualHost = ajustesVm.RabbitVHost;

                AplicarAjustesLlm();

                StatusText = "Conectando...";
                await servicioRabbit.ConnectAsync(ajustesVm.UserName);

                IsConnected = true;
                IsAutoConversing = false;
                StatusText = $"Conectado como {ajustesVm.UserName}";
            }
            catch (Exception ex)
            {
                StatusText = "Error de conexión";
                IsConnected = false;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Messages.Add(new ChatMessage
                    {
                        Sender = "Sistema",
                        Message = $"No se pudo conectar: {ex.Message}",
                        IsFromCurrentUser = false,
                        Timestamp = DateTime.Now
                    });
                });
            }
        }

        //Desconectar del broker
        [RelayCommand]
        private async Task DisconnectAsync()
        {
            try
            {
                await servicioRabbit.DisconnectAsync();
                IsConnected = false;
                IsAutoConversing = false;
                IsOtherTyping = false;
                StatusText = "Desconectado";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        //Limpiar mensajes del chat
        [RelayCommand]
        private void ClearMessages() => Messages.Clear();

        //Enviar mensaje manual
        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(MessageText)) return;

            if (!IsConnected)
            {
                StatusText = "⚠ Pulsa Conectar primero";
                return;
            }

            var texto = MessageText.Trim();
            MessageText = string.Empty;

            try
            {
                await servicioRabbit.PublishMessageAsync(ajustesVm.UserName, texto);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Messages.Add(new ChatMessage
                    {
                        Sender = ajustesVm.UserName,
                        Message = texto,
                        IsFromCurrentUser = true,
                        Timestamp = DateTime.Now
                    });

                    if (AutoModeEnabled)
                        IsAutoConversing = true;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusText = $"Error al enviar: {ex.Message}");
            }
        }

        //Aplicar configuración actual del LLM
        private void AplicarAjustesLlm()
        {
            servicioLlm.UrlBase = ajustesVm.LlmUrl;
            servicioLlm.Model = ajustesVm.Model;
            servicioLlm.SystemPrompt = ajustesVm.SystemPrompt;
            servicioLlm.Temperature = double.TryParse(
                ajustesVm.Temperature,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var temp) ? temp : 0.7;
            servicioLlm.MaxTokens = int.TryParse(
                ajustesVm.MaxTokens,
                out var tokens) ? tokens : 150;
        }

        //Callback al recibir mensaje del otro chat
        private async void AlRecibirMensaje(string remitente, string mensaje)
        {
            try
            {
                //Añadir mensaje recibido
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsOtherTyping = false;
                    Messages.Add(new ChatMessage
                    {
                        Sender = remitente,
                        Message = mensaje,
                        IsFromCurrentUser = false,
                        Timestamp = DateTime.Now
                    });

                    if (AutoModeEnabled)
                        IsAutoConversing = true;
                });

                //Responder automáticamente con el LLM
                if (AutoModeEnabled && !procesando)
                {
                    procesando = true;
                    try
                    {
                        //Notificar que estoy escribiendo...
                        await servicioRabbit.PublishTypingAsync(ajustesVm.UserName, true);

                        await Task.Delay(1500);

                        AplicarAjustesLlm();

                        //Copiar historial de forma segura
                        List<ChatMessage> historial;
                        try { historial = Messages.ToList(); }
                        catch { historial = new List<ChatMessage>(); }

                        var respuesta = await servicioLlm.GetResponseAsync(historial);

                        //Dejar de notificar escritura
                        await servicioRabbit.PublishTypingAsync(ajustesVm.UserName, false);

                        if (!string.IsNullOrEmpty(respuesta))
                        {
                            await servicioRabbit.PublishMessageAsync(ajustesVm.UserName, respuesta);

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                Messages.Add(new ChatMessage
                                {
                                    Sender = ajustesVm.UserName,
                                    Message = respuesta,
                                    IsFromCurrentUser = true,
                                    Timestamp = DateTime.Now
                                });
                                StatusText = $"Conectado como {ajustesVm.UserName}";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        await servicioRabbit.PublishTypingAsync(ajustesVm.UserName, false);
                        MainThread.BeginInvokeOnMainThread(() =>
                            StatusText = $"Error LLM: {ex.Message}");
                    }
                    finally
                    {
                        procesando = false;
                    }
                }
            }
            catch { }
        }
    }
}
