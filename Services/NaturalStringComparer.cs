using System;
using System.Collections.Generic;

namespace LinearCutWpf.Services
{
    /// <summary>
    /// Компаратор для естественной (смешанной числовой) сортировки строк.
    /// Обеспечивает сортировку, при которой числа внутри строк сравниваются как числа,
    /// а не лексикографически. Например: "Арт1", "Арт2", "Арт10" вместо "Арт1", "Арт10", "Арт2".
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        private readonly StringComparison _stringComparison;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="NaturalStringComparer"/>.
        /// </summary>
        /// <param name="ignoreCase">Если true, сравнение проводится без учёта регистра.</param>
        public NaturalStringComparer(bool ignoreCase = true)
        {
            _stringComparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        /// <summary>
        /// Сравнивает две строки с использованием естественной сортировки.
        /// </summary>
        /// <param name="x">Первая строка.</param>
        /// <param name="y">Вторая строка.</param>
        /// <returns>Отрицательное число, если x меньше y; положительное — если больше; 0 — если равны.</returns>
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int ix = 0, iy = 0;

            while (ix < x.Length && iy < y.Length)
            {
                // Пропускаем пробелы в начале сегментов
                while (ix < x.Length && char.IsWhiteSpace(x[ix])) ix++;
                while (iy < y.Length && char.IsWhiteSpace(y[iy])) iy++;

                if (ix >= x.Length || iy >= y.Length) break;

                char cx = x[ix];
                char cy = y[iy];

                // Если оба символа — цифры, извлекаем числовые сегменты
                if (char.IsDigit(cx) && char.IsDigit(cy))
                {
                    int numX = ParseNumber(x, ref ix);
                    int numY = ParseNumber(y, ref iy);

                    if (numX != numY) return numX - numY;
                    continue;
                }

                // Если один символ — цифра, а другой — нет, цифра считается меньше
                if (char.IsDigit(cx)) return -1;
                if (char.IsDigit(cy)) return 1;

                // Оба символа — не цифры, сравниваем текстовые сегменты
                int textResult = CompareTextSegment(x, ref ix, y, ref iy);
                if (textResult != 0) return textResult;
            }

            // Если один из строк закончился, сравниваем длины
            int remainingX = x.Length - ix;
            int remainingY = y.Length - iy;

            if (remainingX != remainingY) return remainingX - remainingY;

            return string.Compare(x, y, _stringComparison);
        }

        /// <summary>
        /// Извлекает число из строки начиная с текущей позиции.
        /// </summary>
        private static int ParseNumber(string s, ref int index)
        {
            int result = 0;
            while (index < s.Length && char.IsDigit(s[index]))
            {
                result = result * 10 + (s[index] - '0');
                index++;
            }
            return result;
        }

        /// <summary>
        /// Сравнивает текстовые сегменты двух строк начиная с текущих позиций.
        /// </summary>
        private int CompareTextSegment(string x, ref int ix, string y, ref int iy)
        {
            // Находим границы текстовых сегментов (до следующей цифры или конца строки)
            int startX = ix;
            int startY = iy;

            while (ix < x.Length && !char.IsDigit(x[ix])) ix++;
            while (iy < y.Length && !char.IsDigit(y[iy])) iy++;

            string segX = x.Substring(startX, ix - startX);
            string segY = y.Substring(startY, iy - startY);

            int result = string.Compare(segX, segY, _stringComparison);
            return result;
        }

        /// <summary>
        /// Статический экземпляр компаратора для повторного использования (без учёта регистра).
        /// </summary>
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer(true);
    }
}