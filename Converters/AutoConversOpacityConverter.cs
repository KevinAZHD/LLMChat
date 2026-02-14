using System.Globalization;

namespace LLMChat.Converters
{
    //Convierte bool a opacidad: true=0.4 (atenuado), false=1.0 (normal)
    public class AutoConversOpacityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool activo && activo ? 0.4 : 1.0;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => false;
    }
}
