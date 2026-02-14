using System.Globalization;

namespace LLMChat.Converters
{
    //Convierte estado de conexión (bool) a color: verde=conectado, rojo=desconectado
    public class ConnectionColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool conectado && conectado ? Color.FromArgb("#4ADE80") : Color.FromArgb("#FF6B6B");

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => false;
    }
}
