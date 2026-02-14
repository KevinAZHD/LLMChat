using System.Text;
using System.Text.Json;

namespace LLMChat.Services
{
    //Servicio de comunicación con el LLM (API OpenAI compatible)
    public class LlmService
    {
        private readonly HttpClient cliente = new() { Timeout = TimeSpan.FromSeconds(60) };

        //Configuración del LLM
        public string UrlBase { get; set; } = "http://localhost:1234";
        public string Model { get; set; } = "default";
        public string SystemPrompt { get; set; } = "Eres un asistente amigable.";
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 150;

        //Obtener lista de modelos disponibles en el servidor
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            var modelos = new List<string>();
            try
            {
                var respuesta = await cliente.GetAsync($"{UrlBase}/v1/models");
                respuesta.EnsureSuccessStatusCode();

                var json = await respuesta.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                foreach (var modelo in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    var id = modelo.GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(id))
                        modelos.Add(id);
                }
            }
            catch { }
            return modelos;
        }

        //Enviar historial de conversación al LLM y obtener respuesta
        public async Task<string> GetResponseAsync(List<Models.ChatMessage> historial)
        {
            try
            {
                //Construir mensajes con el prompt del sistema
                var mensajes = new List<Dictionary<string, string>>
                {
                    new() { ["role"] = "system", ["content"] = SystemPrompt }
                };

                //Convertir historial (filtrar mensajes de sistema)
                foreach (var msg in historial.TakeLast(16))
                {
                    if (msg.Sender == "Sistema") continue;
                    mensajes.Add(new Dictionary<string, string>
                    {
                        ["role"] = msg.IsFromCurrentUser ? "assistant" : "user",
                        ["content"] = msg.Message
                    });
                }

                //La API espera que el primer mensaje tras system sea "user"
                if (mensajes.Count > 1 && mensajes[1]["role"] == "assistant")
                    mensajes[1]["role"] = "user";

                //Si el último mensaje no es "user", no tiene sentido llamar al LLM
                if (mensajes.Count > 1 && mensajes[^1]["role"] != "user")
                    return string.Empty;

                var cuerpo = new
                {
                    model = Model,
                    messages = mensajes,
                    temperature = Temperature,
                    max_tokens = MaxTokens
                };

                var json = JsonSerializer.Serialize(cuerpo);
                var contenido = new StringContent(json, Encoding.UTF8, "application/json");

                var respuesta = await cliente.PostAsync($"{UrlBase}/v1/chat/completions", contenido);
                respuesta.EnsureSuccessStatusCode();

                var respuestaJson = await respuesta.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(respuestaJson);

                var reply = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return reply?.Trim() ?? "(sin respuesta)";
            }
            catch (Exception ex)
            {
                return $"[Error LLM]: {ex.Message}";
            }
        }
    }
}
