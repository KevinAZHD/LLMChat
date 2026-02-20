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

        //Campos JSON conocidos (más robusto: SenderId, Content, etc.)
        private static readonly string[] CamposRemitente = ["sender", "senderId", "sender_id", "user", "userId", "from", "username", "name"];
        private static readonly string[] CamposMensaje = ["message", "content", "text", "body", "msg", "payload"];

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
            try
            {
                //1. Detectar Notificación de Escritura vía HEADERS (Invisible para otros)
                if (ea.BasicProperties.Headers != null &&
                    ea.BasicProperties.Headers.TryGetValue("x-msg-type", out var typeVal))
                {
                    var typeStr = Encoding.UTF8.GetString((byte[])typeVal);
                    if (typeStr == "typing")
                    {
                        var senderName = "Remoto";
                        if (ea.BasicProperties.Headers.TryGetValue("x-sender", out var senderVal))
                            senderName = Encoding.UTF8.GetString((byte[])senderVal);

                        var isTyping = false;
                        if (ea.BasicProperties.Headers.TryGetValue("x-is-typing", out var typingVal))
                            bool.TryParse(Encoding.UTF8.GetString((byte[])typingVal), out isTyping);

                        if (!string.Equals(senderName, usuarioActual, StringComparison.OrdinalIgnoreCase))
                            OnTypingReceived?.Invoke(senderName, isTyping);
                        
                        return; //Importante: No seguir procesando el cuerpo (que está vacío)
                    }
                }

                //2. Procesar Mensaje Normal (Texto/JSON)
                var texto = Encoding.UTF8.GetString(ea.Body.ToArray());
                if (string.IsNullOrWhiteSpace(texto)) return; //Ignorar mensajes vacíos

                using var doc = JsonDocument.Parse(texto);
                var root = doc.RootElement;
                
                var remitente = ExtraerCampo(root, CamposRemitente) ?? "Remoto";
                var tipo = ExtraerCampo(root, ["type"]) ?? "message";

                if (string.Equals(remitente, usuarioActual, StringComparison.OrdinalIgnoreCase))
                    return;

                //Compatibilidad con apps antiguas que mandan typing por JSON
                if (tipo == "typing" && root.TryGetProperty("isTyping", out var typingEl))
                {
                    OnTypingReceived?.Invoke(remitente, typingEl.GetBoolean());
                }
                else
                {
                    var contenido = ExtraerCampo(root, CamposMensaje) ?? texto;
                    var mensajeLimpio = LimpiarMensaje(contenido);
                    
                    if (!string.IsNullOrEmpty(mensajeLimpio))
                        OnMessageReceived?.Invoke(remitente, mensajeLimpio);
                }
            }
            catch (JsonException)
            {
                var texto = Encoding.UTF8.GetString(ea.Body.ToArray());
                //Ignorar vacíos
                if(string.IsNullOrWhiteSpace(texto)) return;

                var (remitente, mensaje) = SepararTextoPlano(texto);
                mensaje = LimpiarMensaje(mensaje);

                if (!string.Equals(remitente, usuarioActual, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(mensaje))
                    OnMessageReceived?.Invoke(remitente, mensaje);
            }
            catch
            {
                 //Fallback seguro
            }
        }

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

        //Publicar notificación de "escribiendo..." (VERSIÓN SILENCIOSA VÍA HEADERS)
        public async Task PublishTypingAsync(string remitente, bool escribiendo)
        {
            if (canal == null) return;

            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object>
                {
                    { "x-msg-type", "typing" },
                    { "x-sender", remitente },
                    { "x-is-typing", escribiendo.ToString() }
                }
            };

            //Enviamos cuerpo VACÍO.
            //La app leerá los headers y mostrará el indicador.
            await canal.BasicPublishAsync(ExchangeName, "", false, props, ReadOnlyMemory<byte>.Empty);
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

        //Extraer primer campo JSON que coincida (insensible a mayúsculas)
        private static string? ExtraerCampo(JsonElement root, string[] campos)
        {
            //Busca propiedades que coincidan con la lista, ignorando mayúsculas/minúsculas
            foreach (var prop in root.EnumerateObject())
            {
                if (campos.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                {
                     return prop.Value.ValueKind == JsonValueKind.String 
                        ? prop.Value.GetString() 
                        : prop.Value.GetRawText().Trim('"');
                }
            }
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
