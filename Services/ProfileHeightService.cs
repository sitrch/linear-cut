using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LinearCutWpf.Models;

namespace LinearCutWpf.Services
{
    /// <summary>
    /// Сервис для сохранения и загрузки данных о видимой высоте профилей.
    /// </summary>
    public static class ProfileHeightService
    {
        /// <summary>
        /// Путь к файлу с данными о высоте профилей.
        /// </summary>
        private static string HeightsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Высота профилей.xml");
        
        /// <summary>
        /// Позволяет переопределить путь к файлу для тестирования.
        /// </summary>
        public static string OverrideFilePath { get; set; }
        
        /// <summary>
        /// Получает текущий путь к файлу с данными.
        /// </summary>
        private static string GetCurrentFilePath() => OverrideFilePath ?? HeightsFilePath;

        /// <summary>
        /// Загружает данные о высоте профилей и длине хлыста из XML-файла.
        /// </summary>
        /// <returns>Словарь, где ключ - артикул, значение - кортеж (высота, является значением по умолчанию, изменено вручную, длина хлыста, длина хлыста изменена вручную).</returns>
        public static Dictionary<string, (double? height, bool isDefaultValue, bool isManuallyChanged, double? barLength, bool isBarLengthManuallyChanged)> LoadProfileHeightsWithMetadata()
        {
            var heights = new Dictionary<string, (double?, bool, bool, double?, bool)>();

            string filePath = GetCurrentFilePath();
            if (!File.Exists(filePath))
            {
                return heights;
            }

            try
            {
                var doc = XDocument.Load(filePath);
                var root = doc.Root;
                
                var profilesElement = root?.Element("Profiles");
                if (profilesElement != null)
                {
                    foreach (var profileElement in profilesElement.Elements("Profile"))
                    {
                        var article = profileElement.Attribute("Article")?.Value;
                        var heightValue = profileElement.Attribute("VisibleHeight")?.Value;
                        var isDefaultValueAttr = profileElement.Attribute("IsDefaultValue");
                        var isManuallyChangedAttr = profileElement.Attribute("IsManuallyChanged");
                        var barLengthAttr = profileElement.Attribute("BarLength");
                        var isBarLengthManuallyChangedAttr = profileElement.Attribute("IsBarLengthManuallyChanged");
                        
                        if (!string.IsNullOrEmpty(article))
                        {
                            double? height = null;
                            // Проверяем, является ли значение "null"
                            if (heightValue == "null")
                            {
                                height = null;
                            }
                            else if (double.TryParse(heightValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedHeight))
                            {
                                height = parsedHeight;
                            }
                            
                            bool isDefaultValue = false;
                            if (isDefaultValueAttr != null && bool.TryParse(isDefaultValueAttr.Value, out bool parsedIsDefaultValue))
                            {
                                isDefaultValue = parsedIsDefaultValue;
                            }
                            
                            bool isManuallyChanged = false;
                            if (isManuallyChangedAttr != null && bool.TryParse(isManuallyChangedAttr.Value, out bool parsedIsManuallyChanged))
                            {
                                isManuallyChanged = parsedIsManuallyChanged;
                            }

                            double? barLength = null;
                            if (barLengthAttr != null)
                            {
                                if (barLengthAttr.Value == "null")
                                {
                                    barLength = null;
                                }
                                else if (double.TryParse(barLengthAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedBarLength))
                                {
                                    barLength = parsedBarLength;
                                }
                            }

                            bool isBarLengthManuallyChanged = false;
                            if (isBarLengthManuallyChangedAttr != null && bool.TryParse(isBarLengthManuallyChangedAttr.Value, out bool parsedIsBarLengthManuallyChanged))
                            {
                                isBarLengthManuallyChanged = parsedIsBarLengthManuallyChanged;
                            }
                            
                            heights[article] = (height, isDefaultValue, isManuallyChanged, barLength, isBarLengthManuallyChanged);
                        }
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем пустой словарь
                heights.Clear();
            }

            return heights;
        }

        /// <summary>
        /// Сохраняет данные о высоте профилей в XML-файл.
        /// </summary>
        /// <param name="profileRows">Коллекция строк с данными о высотах профилей.</param>
        /// <param name="defaultHeight">Высота по умолчанию.</param>
        public static void SaveProfileHeightsWithMetadata(IEnumerable<ArticleGroupingRow> profileRows, double? defaultHeight)
        {
            try
            {
                // Проверяем, есть ли данные для сохранения
                var rowsToSave = profileRows?.Where(r => !string.IsNullOrEmpty(r.ArticleName) && 
                    (r.SelectedVisibleHeight.HasValue || r.IsDefaultValue || r.IsManuallyChanged ||
                     r.SelectedBarLength.HasValue || r.IsBarLengthManuallyChanged)).ToList();
                
                string filePath = GetCurrentFilePath();

                // Если нет данных для сохранения, ничего не делаем
                if ((rowsToSave == null || rowsToSave.Count == 0) && !defaultHeight.HasValue)
                {
                    return;
                }

                XDocument doc;
                XElement root;

                if (File.Exists(filePath))
                {
                    try
                    {
                        doc = XDocument.Load(filePath);
                        root = doc.Root;
                        if (root == null || root.Name != "ProfileHeights")
                        {
                            root = new XElement("ProfileHeights");
                            doc = new XDocument(root);
                        }
                    }
                    catch
                    {
                        root = new XElement("ProfileHeights");
                        doc = new XDocument(root);
                    }
                }
                else
                {
                    root = new XElement("ProfileHeights");
                    doc = new XDocument(root);
                }

                // Сохраняем высоту по умолчанию
                if (defaultHeight.HasValue)
                {
                    root.SetAttributeValue("DefaultHeight", defaultHeight.Value.ToString(CultureInfo.InvariantCulture));
                }

                // Добавляем или обновляем Profiles
                if (rowsToSave != null && rowsToSave.Count > 0)
                {
                    var profilesElement = root.Element("Profiles");
                    if (profilesElement == null)
                    {
                        profilesElement = new XElement("Profiles");
                        root.Add(profilesElement);
                    }

                    foreach (var row in rowsToSave)
                    {
                        string heightValue = row.SelectedVisibleHeight?.ToString(CultureInfo.InvariantCulture) ?? "null";
                        
                        var existingProfile = profilesElement.Elements("Profile")
                            .FirstOrDefault(e => e.Attribute("Article")?.Value == row.ArticleName);

                        string barLengthValue = row.SelectedBarLength.HasValue && row.SelectedBarLength.Value > 0
                            ? row.SelectedBarLength.Value.ToString(CultureInfo.InvariantCulture)
                            : "null";

                        if (existingProfile != null)
                        {
                            existingProfile.SetAttributeValue("VisibleHeight", heightValue);
                            existingProfile.SetAttributeValue("IsDefaultValue", row.IsDefaultValue.ToString());
                            existingProfile.SetAttributeValue("IsManuallyChanged", row.IsManuallyChanged.ToString());
                            existingProfile.SetAttributeValue("BarLength", barLengthValue);
                            existingProfile.SetAttributeValue("IsBarLengthManuallyChanged", row.IsBarLengthManuallyChanged.ToString());
                        }
                        else
                        {
                            profilesElement.Add(new XElement("Profile",
                                new XAttribute("Article", row.ArticleName),
                                new XAttribute("VisibleHeight", heightValue),
                                new XAttribute("IsDefaultValue", row.IsDefaultValue.ToString()),
                                new XAttribute("IsManuallyChanged", row.IsManuallyChanged.ToString()),
                                new XAttribute("BarLength", barLengthValue),
                                new XAttribute("IsBarLengthManuallyChanged", row.IsBarLengthManuallyChanged.ToString())));
                        }
                    }
                }

                doc.Save(filePath);
            }
            catch (Exception ex)
            {
                // Выводим ошибку в консоль для отладки
                System.Console.WriteLine($"[ProfileHeightService] Error saving profile heights: {ex}");
                System.Diagnostics.Debug.WriteLine($"[ProfileHeightService] Error saving profile heights: {ex}");
            }
        }

        /// <summary>
        /// Загружает высоту по умолчанию из файла.
        /// </summary>
        /// <returns>Высота по умолчанию или null, если не задана.</returns>
        public static double? LoadDefaultHeight()
        {
            if (!File.Exists(GetCurrentFilePath()))
            {
                return null;
            }

            try
            {
                var doc = XDocument.Load(GetCurrentFilePath());
                var defaultHeightAttr = doc.Root?.Attribute("DefaultHeight");
                
                if (defaultHeightAttr != null && double.TryParse(defaultHeightAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double defaultHeight))
                {
                    return defaultHeight;
                }
            }
            catch
            {
                // Игнорируем ошибки
            }

            return null;
        }
    }
}