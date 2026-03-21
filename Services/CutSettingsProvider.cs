using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LinearCutWpf.Models;

namespace LinearCutWpf.Services
{
    public static class CutSettingsProvider
    {
        private static string XmlPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

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

        public static string LoadDefaultPresetName()
        {
            if (!File.Exists(XmlPath)) return null;
            return XDocument.Load(XmlPath).Root.Element("DefaultPreset")?.Attribute("Name")?.Value;
        }

        public static void SaveDefaultPresetName(string presetName)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var el = doc.Root.Element("DefaultPreset");
            if (el != null) el.Remove();
            doc.Root.Add(new XElement("DefaultPreset", new XAttribute("Name", presetName ?? "")));
            doc.Save(XmlPath);
        }

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

        public static double LoadDefaultStockLength()
        {
            if (!File.Exists(XmlPath)) return 6000;
            var value = XDocument.Load(XmlPath).Root.Element("DefaultStockLength")?.Attribute("Value")?.Value;
            return double.TryParse(value, out var len) ? len : 6000;
        }

        public static void SaveDefaultStockLength(double length)
        {
            var doc = File.Exists(XmlPath) ? XDocument.Load(XmlPath) : new XDocument(new XElement("Settings"));
            var el = doc.Root.Element("DefaultStockLength");
            if (el != null) el.Remove();
            doc.Root.Add(new XElement("DefaultStockLength", new XAttribute("Value", length)));
            doc.Save(XmlPath);
        }
    }
}