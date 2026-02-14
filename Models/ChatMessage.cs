namespace LLMChat.Models
{
    //Modelo de mensaje del chat
    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsFromCurrentUser { get; set; }
    }
}
