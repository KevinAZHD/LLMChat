using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        public string RabbitUser { get; set; } = "guest";
        public string RabbitPassword { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";

        //Eventos
        public event Action<string, string>? OnMessageReceived;
        public event Action<string, bool>? OnTypingReceived;

        //Campos JSON conocidos para extraer remitente y contenido
        private static readonly string[] CamposRemitente = ["sender", "user", "from", "username", "name"];
        private static readonly string[] CamposMensaje = ["message", "content", "text", "body", "msg"];

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
                    UserName = RabbitUser,
                    Password = RabbitPassword,
                    VirtualHost = VirtualHost
                };

                conexion = await factory.CreateConnectionAsync();
                canal = await conexion.CreateChannelAsync();

                await canal.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, durable: false, autoDelete: false);

                var resultado = await canal.QueueDeclareAsync("", durable: false, exclusive: true, autoDelete: true);
                nombreCola = resultado.QueueName;

                await canal.QueueBindAsync(nombreCola, ExchangeName, "");

                var consumidor = new AsyncEventingBasicConsumer(canal);
                consumidor.ReceivedAsync += ProcesarMensajeAsync;

                tagConsumidor = await canal.BasicConsumeAsync(nombreCola, autoAck: true, consumer: consumidor);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error conectando a RabbitMQ: {ex.Message}", ex);
            }
        }

        //Procesar mensaje recibido del exchange
        private async Task ProcesarMensajeAsync(object model, BasicDeliverEventArgs ea)
        {
            var texto = Encoding.UTF8.GetString(ea.Body.ToArray());

            try
            {
                using var doc = JsonDocument.Parse(texto);
                var root = doc.RootElement;

                var remitente = ExtraerCampo(root, CamposRemitente) ?? "Remoto";
                var tipo = ExtraerCampo(root, ["type"]) ?? "message";

                if (string.Equals(remitente, usuarioActual, StringComparison.OrdinalIgnoreCase))
                    return;

                if (tipo == "typing" && root.TryGetProperty("isTyping", out var typingEl))
                    OnTypingReceived?.Invoke(remitente, typingEl.GetBoolean());
                else
                {
                    var mensaje = LimpiarMensaje(ExtraerCampo(root, CamposMensaje) ?? texto);
                    if (!string.IsNullOrEmpty(mensaje))
                        OnMessageReceived?.Invoke(remitente, mensaje);
                }
            }
            catch (JsonException)
            {
                //Texto plano — extraer "nombre: mensaje" si aplica
                var (remitente, mensaje) = SepararTextoPlano(texto);
                mensaje = LimpiarMensaje(mensaje);

                if (!string.Equals(remitente, usuarioActual, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(mensaje))
                    OnMessageReceived?.Invoke(remitente, mensaje);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(texto))
                    OnMessageReceived?.Invoke("Remoto", texto);
            }

            await Task.CompletedTask;
        }

        //Publicar mensaje de chat
        public async Task PublishMessageAsync(string remitente, string mensaje)
        {
            if (canal == null) throw new InvalidOperationException("No conectado a RabbitMQ.");

            var json = JsonSerializer.Serialize(new
            {
                type = "message",
                sender = remitente,
                message = mensaje,
                timestamp = DateTime.UtcNow.ToString("o")
            });

            await canal.BasicPublishAsync(ExchangeName, "", Encoding.UTF8.GetBytes(json));
        }

        //Publicar notificación de "escribiendo..."
        public async Task PublishTypingAsync(string remitente, bool escribiendo)
        {
            if (canal == null) return;

            var json = JsonSerializer.Serialize(new { type = "typing", sender = remitente, isTyping = escribiendo });
            await canal.BasicPublishAsync(ExchangeName, "", Encoding.UTF8.GetBytes(json));
        }

        //Desconectar del broker
        public async Task DisconnectAsync()
        {
            if (canal != null)
            {
                if (!string.IsNullOrEmpty(tagConsumidor))
                    try { await canal.BasicCancelAsync(tagConsumidor); } catch { }
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

        //── Helpers ──────────────────────────────────────────────────────

        //Extraer primer campo JSON que exista de una lista de nombres
        private static string? ExtraerCampo(JsonElement root, string[] campos)
        {
            foreach (var campo in campos)
                if (root.TryGetProperty(campo, out var el))
                    return el.GetString();
            return null;
        }

        //Separar texto plano con formato "nombre: mensaje"
        private static (string remitente, string mensaje) SepararTextoPlano(string texto)
        {
            var idx = texto.IndexOf(": ");
            if (idx > 0 && idx < 30)
                return (texto[..idx], texto[(idx + 2)..]);
            return ("Remoto", texto);
        }

        //Limpiar artefactos de LLM del mensaje recibido
        private static string LimpiarMensaje(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return texto;
            var limpio = texto.Trim();

            //[TOOL_CALLS]{"id":"2","content":"texto real"}
            if (limpio.StartsWith("[TOOL_CALLS]", StringComparison.OrdinalIgnoreCase))
            {
                var jsonPart = limpio["[TOOL_CALLS]".Length..].Trim();
                limpio = ExtraerContenidoJson(jsonPart) ?? jsonPart;
            }

            //{"function_call":{"arguments":"{\"content\":\"texto\"}"}} o {"content":"texto"}
            if (limpio.StartsWith('{') && limpio.EndsWith('}'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(limpio);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("function_call", out var fc)
                        && fc.TryGetProperty("arguments", out var args))
                    {
                        var inner = ExtraerContenidoJson(args.GetString() ?? "");
                        if (inner != null) limpio = inner;
                    }
                    else
                    {
                        var inner = ExtraerCampo(root, ["content", "message", "text"]);
                        if (inner != null) limpio = inner;
                    }
                }
                catch { }
            }

            //Decodificar unicode escapados (\u00E1 → á)
            if (limpio.Contains("\\u00"))
                try { limpio = Regex.Unescape(limpio); } catch { }

            return limpio.Trim();
        }

        //Intentar extraer content/message/text de un string JSON
        private static string? ExtraerContenidoJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return ExtraerCampo(doc.RootElement, ["content", "message", "text"]);
            }
            catch { return null; }
        }
    }
}
