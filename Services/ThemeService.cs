namespace LLMChat.Services
{
    //Gestiona los colores dinámicos de la interfaz (modo oscuro/claro y burbujas)
    public class ThemeService
    {
        //Colores modo oscuro
        public const string DarkPageBg = "#0F0F1A";
        public const string DarkHeaderBg = "#1A1A2E";
        public const string DarkCardBg = "#1A1A2E";
        public const string DarkInputBg = "#252540";
        public const string DarkPrimaryText = "#EAEAEA";
        public const string DarkSecondaryText = "#8888AA";
        public const string DarkBorderColor = "#2D2D4A";
        public const string DarkHeaderText = "#C8C8E0";
        public const string DarkPlaceholderText = "#5A5A7A";
        public const string DarkAccentColor = "#00D2FF";

        //Colores modo claro
        public const string LightPageBg = "#F5F5FA";
        public const string LightHeaderBg = "#FFFFFF";
        public const string LightCardBg = "#FFFFFF";
        public const string LightInputBg = "#F0F0F5";
        public const string LightPrimaryText = "#1A1A2E";
        public const string LightSecondaryText = "#6E6E8A";
        public const string LightBorderColor = "#D0D0E0";
        public const string LightHeaderText = "#2A2A4A";
        public const string LightPlaceholderText = "#9898B0";
        public const string LightAccentColor = "#007A99";

        //Colores de burbujas por defecto
        public const string DefaultSentBubble = "#6C5CE7";
        public const string DefaultReceivedBubble = "#2A2A4A";

        //Aplicar tema según preferencias guardadas
        public void ApplyTheme()
        {
            var res = Application.Current?.Resources;
            if (res == null) return;

            bool oscuro = Preferences.Get("IsDarkMode", true);

            //Colores según modo
            Set(res, "PageBackground", oscuro ? DarkPageBg : LightPageBg);
            Set(res, "HeaderBackground", oscuro ? DarkHeaderBg : LightHeaderBg);
            Set(res, "CardBackground", oscuro ? DarkCardBg : LightCardBg);
            Set(res, "InputBackground", oscuro ? DarkInputBg : LightInputBg);
            Set(res, "PrimaryText", oscuro ? DarkPrimaryText : LightPrimaryText);
            Set(res, "SecondaryText", oscuro ? DarkSecondaryText : LightSecondaryText);
            Set(res, "BorderColor", oscuro ? DarkBorderColor : LightBorderColor);
            Set(res, "HeaderText", oscuro ? DarkHeaderText : LightHeaderText);
            Set(res, "PlaceholderText", oscuro ? DarkPlaceholderText : LightPlaceholderText);
            Set(res, "AccentColor", oscuro ? DarkAccentColor : LightAccentColor);

            //Colores de burbujas (independientes del modo)
            var hexEnviado = Preferences.Get("SentBubbleColor", DefaultSentBubble);
            var hexRecibido = Preferences.Get("ReceivedBubbleColor", DefaultReceivedBubble);
            Set(res, "SentBubble", hexEnviado);
            Set(res, "ReceivedBubble", hexRecibido);

            //Texto adaptativo por contraste
            res["SentBubbleText"] = ContrastText(hexEnviado);
            res["ReceivedBubbleText"] = ContrastText(hexRecibido);
        }

        //Devuelve blanco o negro según luminancia del fondo
        public static Color ContrastText(string hex)
        {
            try
            {
                var c = Color.FromArgb(hex.StartsWith('#') ? hex : $"#{hex}");
                double luminancia = 0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue;
                return luminancia > 0.5 ? Colors.Black : Colors.White;
            }
            catch { return Colors.White; }
        }

        //Asignar color hex a un recurso del diccionario
        private static void Set(ResourceDictionary res, string clave, string hex)
        {
            try { res[clave] = Color.FromArgb(hex.StartsWith('#') ? hex : $"#{hex}"); }
            catch { }
        }
    }
}
