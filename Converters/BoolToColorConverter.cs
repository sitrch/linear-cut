using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LinearCutWpf.Converters
{
    /// <summary>
    /// Конвертер для преобразования логического значения в цвет кисти.
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        /// <summary>
        /// Преобразует логическое значение в кисть (<see cref="Brushes.LightGreen"/> для true, иначе <see cref="Brushes.White"/>).
        /// </summary>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="targetType">Целевой тип данных.</param>
        /// <param name="parameter">Дополнительный параметр конвертера.</param>
        /// <param name="culture">Культура.</param>
        /// <returns>Кисть, соответствующая значению.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
                return Brushes.LightGreen;
            return Brushes.White;
        }

        /// <summary>
        /// Обратное преобразование (не реализовано).
        /// </summary>
        /// <exception cref="NotImplementedException">Всегда выбрасывает исключение, так как обратное преобразование не поддерживается.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
