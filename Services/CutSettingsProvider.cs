using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LinearCutWpf.Models;

namespace LinearCutWpf.Services
{
    /// <summary>
    /// Статический класс для сохранения и загрузки настроек приложения (пресеты, длины хлыстов, размеры окна) из XML-файла.
    /// </summary>
    public static class CutSettingsProvider
    {
        /// <summary>
        /// Путь к файлу настроек (settings.xml в директории приложения).
        /// </summary>
        private static string XmlPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

        /// <summary>
        /// Загружает список всех сохраненных пресетов настроек реза.
        /// </summary>
        /// <returns>Список моделей пресетов.</returns>
        public static List<PresetModel> LoadAll()
        {
            if (!File.Exists(XmlPath)) return new List<PresetModel> { new PresetModel { Name = "Default", TrimStart = 10, TrimEnd = 10, CutWidth = 4 } };

            return XDocument.Load(XmlPath).Root.Element("Presets")?.Elements("Preset")
                .Select(p => new PresetModel
                {
                    Name = p.Attribute("Name")?.Value,
                    TrimStart = double.Parse(p.Element("TrimStart")?.Value ?? "0"),
                    TrimEnd = double.Parse(p.Element("TrimEnd")?.Value ?? "0"),
                    CutWidth = double.Parse(p.Element("CutWidth")?.Value ?? "0")
                }).ToList() ?? new List<PresetModel>();
        }

        /// <summary>
        /// Сохраняет список пресетов в файл настроек.
        /// </summary>
        /// <param name="presets">Список пресетов для сохранения.</param>
        public static void SaveAll(List<PresetModel> presets)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var presetsElement = doc.Root.Element("Presets");
            if (presetsElement != null)
                presetsElement.Remove();
            doc.Root.Add(new XElement("Presets",
                presets.Select(p => new XElement("Preset",
                    new XAttribute("Name", p.Name),
                    new XElement("TrimStart", p.TrimStart),
                    new XElement("TrimEnd", p.TrimEnd),
                    new XElement("CutWidth", p.CutWidth)
                ))
            ));
            doc.Save(XmlPath);
        }

        /// <summary>
        /// Загружает имя пресета по умолчанию.
        /// </summary>
        /// <returns>Имя пресета по умолчанию или null, если файл не существует.</returns>
        public static string LoadDefaultPresetName()
        {
            if (!File.Exists(XmlPath)) return null;
            return XDocument.Load(XmlPath).Root.Element("DefaultPreset")?.Attribute("Name")?.Value;
        }

        /// <summary>
        /// Сохраняет имя пресета, выбранного по умолчанию.
        /// </summary>
        /// <param name="presetName">Имя пресета.</param>
        public static void SaveDefaultPresetName(string presetName)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var el = doc.Root.Element("DefaultPreset");
            if (el != null) el.Remove();
            doc.Root.Add(new XElement("DefaultPreset", new XAttribute("Name", presetName ?? "")));
            doc.Save(XmlPath);
        }

        /// <summary>
        /// Загружает список доступных длин хлыстов.
        /// </summary>
        /// <returns>Список моделей длин хлыстов.</returns>
        public static List<StockLengthModel> LoadStockLengths()
        {
            if (!File.Exists(XmlPath)) return new List<StockLengthModel> { new StockLengthModel { Length = 6000, IsEnabled = true } };

            return XDocument.Load(XmlPath).Root.Element("StockLengths")?.Elements("StockLength")
                .Select(s => new StockLengthModel
                {
                    Length = double.Parse(s.Attribute("Length")?.Value ?? "6000"),
                    IsEnabled = bool.Parse(s.Attribute("IsEnabled")?.Value ?? "true")
                }).ToList() ?? new List<StockLengthModel> { new StockLengthModel { Length = 6000, IsEnabled = true } };
        }

        /// <summary>
        /// Сохраняет список доступных длин хлыстов в файл настроек.
        /// </summary>
        /// <param name="stockLengths">Список длин хлыстов.</param>
        public static void SaveStockLengths(List<StockLengthModel> stockLengths)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var stockElement = doc.Root.Element("StockLengths");
            if (stockElement != null)
                stockElement.Remove();
            doc.Root.Add(new XElement("StockLengths",
                stockLengths.Select(s => new XElement("StockLength",
                    new XAttribute("Length", s.Length),
                    new XAttribute("IsEnabled", s.IsEnabled)
                ))
            ));
            doc.Save(XmlPath);
        }

        /// <summary>
        /// Загружает длину хлыста по умолчанию.
        /// </summary>
        /// <returns>Длина хлыста (по умолчанию 6000).</returns>
        public static double LoadDefaultStockLength()
        {
            if (!File.Exists(XmlPath)) return 6000;
            var value = XDocument.Load(XmlPath).Root.Element("DefaultStockLength")?.Attribute("Value")?.Value;
            return double.TryParse(value, out var len) ? len : 6000;
        }

        /// <summary>
        /// Сохраняет длину хлыста по умолчанию.
        /// </summary>
        /// <param name="length">Длина хлыста.</param>
        public static void SaveDefaultStockLength(double length)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var el = doc.Root.Element("DefaultStockLength");
            if (el != null) el.Remove();
            doc.Root.Add(new XElement("DefaultStockLength", new XAttribute("Value", length)));
            doc.Save(XmlPath);
        }

        /// <summary>
        /// Загружает ширину левой панели главного окна.
        /// </summary>
        /// <returns>Ширина панели (по умолчанию 450).</returns>
        public static double LoadLeftPanelWidth()
        {
            if (!File.Exists(XmlPath)) return 450;
            var value = XDocument.Load(XmlPath).Root.Element("LeftPanelWidth")?.Attribute("Value")?.Value;
            return double.TryParse(value, out var width) ? width : 450;
        }

        /// <summary>
        /// Сохраняет ширину левой панели главного окна.
        /// </summary>
        /// <param name="width">Ширина панели.</param>
        public static void SaveLeftPanelWidth(double width)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var el = doc.Root.Element("LeftPanelWidth");
            if (el != null) el.Remove();
            doc.Root.Add(new XElement("LeftPanelWidth", new XAttribute("Value", width)));
            doc.Save(XmlPath);
        }

        /// <summary>
        /// Модель для хранения настроек главного окна (положение и размеры).
        /// </summary>
        public class WindowSettings
        {
            public double Left { get; set; } = double.NaN;
            public double Top { get; set; } = double.NaN;
            public double Width { get; set; } = 1000;
            public double Height { get; set; } = 700;
            public string WindowState { get; set; } = "Normal";
        }

        /// <summary>
        /// Загружает настройки главного окна (размеры, положение, состояние).
        /// </summary>
        /// <returns>Объект с настройками окна.</returns>
        public static WindowSettings LoadWindowSettings()
        {
            var settings = new WindowSettings();
            if (!File.Exists(XmlPath)) return settings;

            var el = XDocument.Load(XmlPath).Root.Element("WindowSettings");
            if (el != null)
            {
                if (double.TryParse(el.Attribute("Left")?.Value, out double left)) settings.Left = left;
                if (double.TryParse(el.Attribute("Top")?.Value, out double top)) settings.Top = top;
                if (double.TryParse(el.Attribute("Width")?.Value, out double width)) settings.Width = width;
                if (double.TryParse(el.Attribute("Height")?.Value, out double height)) settings.Height = height;
                settings.WindowState = el.Attribute("WindowState")?.Value ?? "Normal";
            }
            return settings;
        }

        /// <summary>
        /// Сохраняет настройки главного окна.
        /// </summary>
        /// <param name="settings">Объект с настройками окна.</param>
        public static void SaveWindowSettings(WindowSettings settings)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var el = doc.Root.Element("WindowSettings");
            if (el != null) el.Remove();
            doc.Root.Add(new XElement("WindowSettings",
                new XAttribute("Left", settings.Left),
                new XAttribute("Top", settings.Top),
                new XAttribute("Width", settings.Width),
                new XAttribute("Height", settings.Height),
                new XAttribute("WindowState", settings.WindowState)
            ));
            doc.Save(XmlPath);
        }

        /// <summary>
        /// Загружает коэффициент визуальной высоты хлыста для PDF.
        /// </summary>
        /// <returns>Коэффициент (по умолчанию 1.0).</returns>
        public static double LoadVisualPdfHeightCoefficient()
        {
            if (!File.Exists(XmlPath)) return 1.0;
            var value = XDocument.Load(XmlPath).Root.Element("VisualPdfHeightCoefficient")?.Attribute("Value")?.Value;
            return double.TryParse(value, out var coeff) ? coeff : 1.0;
        }

        /// <summary>
        /// Сохраняет коэффициент визуальной высоты хлыста для PDF.
        /// </summary>
        /// <param name="coefficient">Коэффициент.</param>
        public static void SaveVisualPdfHeightCoefficient(double coefficient)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var el = doc.Root.Element("VisualPdfHeightCoefficient");
            if (el != null) el.Remove();
            doc.Root.Add(new XElement("VisualPdfHeightCoefficient", new XAttribute("Value", coefficient)));
            doc.Save(XmlPath);
        }
    }
}
