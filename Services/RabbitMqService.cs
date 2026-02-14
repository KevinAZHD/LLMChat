using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace LLMChat.Services
{
    //Servicio de mensajería RabbitMQ (pub/sub fanout)
    public class RabbitMqService : IAsyncDisposable
    {
        private IConnection? conexion;
        private IChannel? canal;
        private string nombreCola = string.Empty;
        private string tagConsumidor = string.Empty;
        private string usuarioActual = string.Empty;

        //Configuración del broker
        public string BrokerHost { get; set; } = "localhost";
        public int BrokerPort { get; set; } = 5672;
        public string ExchangeName { get; set; } = "llmchat_exchange";

        //Eventos de mensajes y escritura
        public event Action<string, string>? OnMessageReceived;
        public event Action<string, bool>? OnTypingReceived;

        //Conectar al broker y suscribirse al exchange
        public async Task ConnectAsync(string nombreUsuario)
        {
            try
            {
                usuarioActual = nombreUsuario;

                var factory = new ConnectionFactory
                {
                    HostName = BrokerHost,
                    Port = BrokerPort,
                    UserName = "guest",
                    Password = "guest"
                };

                conexion = await factory.CreateConnectionAsync();
                canal = await conexion.CreateChannelAsync();

                //Declarar exchange fanout
                await canal.ExchangeDeclareAsync(
                    exchange: ExchangeName,
                    type: ExchangeType.Fanout,
                    durable: false,
                    autoDelete: false);

                //Crear cola exclusiva temporal
                var resultado = await canal.QueueDeclareAsync(
                    queue: string.Empty,
                    durable: false,
                    exclusive: true,
                    autoDelete: true);

                nombreCola = resultado.QueueName;

                await canal.QueueBindAsync(
                    queue: nombreCola,
                    exchange: ExchangeName,
                    routingKey: string.Empty);

                //Consumidor asíncrono
                var consumidor = new AsyncEventingBasicConsumer(canal);
                consumidor.ReceivedAsync += async (model, ea) =>
                {
                    var cuerpo = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(cuerpo);

                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var remitente = doc.RootElement.GetProperty("sender").GetString() ?? "Unknown";
                        var tipo = doc.RootElement.TryGetProperty("type", out var tipoEl)
                            ? tipoEl.GetString() ?? "message"
                            : "message";

                        //No procesar nuestros propios mensajes
                        if (remitente != usuarioActual)
                        {
                            if (tipo == "typing")
                            {
                                var escribiendo = doc.RootElement.GetProperty("isTyping").GetBoolean();
                                OnTypingReceived?.Invoke(remitente, escribiendo);
                            }
                            else
                            {
                                var mensaje = doc.RootElement.GetProperty("message").GetString() ?? "";
                                OnMessageReceived?.Invoke(remitente, mensaje);
                            }
                        }
                    }
                    catch { }

                    await Task.CompletedTask;
                };

                tagConsumidor = await canal.BasicConsumeAsync(
                    queue: nombreCola,
                    autoAck: true,
                    consumer: consumidor);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error conectando a RabbitMQ: {ex.Message}", ex);
            }
        }

        //Publicar mensaje de chat
        public async Task PublishMessageAsync(string remitente, string mensaje)
        {
            if (canal == null)
                throw new InvalidOperationException("No conectado a RabbitMQ.");

            var payload = new
            {
                type = "message",
                sender = remitente,
                message = mensaje,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(payload);
            var cuerpo = Encoding.UTF8.GetBytes(json);

            await canal.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: string.Empty,
                body: cuerpo);
        }

        //Publicar notificación de "escribiendo..."
        public async Task PublishTypingAsync(string remitente, bool escribiendo)
        {
            if (canal == null) return;

            var payload = new { type = "typing", sender = remitente, isTyping = escribiendo };
            var json = JsonSerializer.Serialize(payload);
            var cuerpo = Encoding.UTF8.GetBytes(json);

            await canal.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: string.Empty,
                body: cuerpo);
        }

        //Desconectar del broker
        public async Task DisconnectAsync()
        {
            if (canal != null)
            {
                if (!string.IsNullOrEmpty(tagConsumidor))
                {
                    try { await canal.BasicCancelAsync(tagConsumidor); } catch { }
                }
                await canal.CloseAsync();
                canal = null;
            }

            if (conexion != null)
            {
                await conexion.CloseAsync();
                conexion = null;
            }
        }

        //Estado de conexión
        public bool IsConnected => conexion is { IsOpen: true };

        //Liberar recursos
        public async ValueTask DisposeAsync() => await DisconnectAsync();
    }
}
