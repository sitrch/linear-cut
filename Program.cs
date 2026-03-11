using System;
using System.Windows.Forms;

namespace LinearCutOptimization
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread] // Обязательно для корректной работы Windows-диалогов
        static void Main()
        {
            // Включаем современные визуальные стили (скругленные кнопки и т.д.)
            Application.EnableVisualStyles();

            // Настраиваем корректный рендеринг текста
            Application.SetCompatibleTextRenderingDefault(false);

            // Создаем экземпляр нашей основной формы и запускаем цикл обработки сообщений
            // Если вы назвали класс формы MainForm (как в последнем коде), используем это имя
            Application.Run(new MainForm());
        }
    }
}
