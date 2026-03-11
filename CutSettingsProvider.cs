using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace LinearCutOptimization
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
            var doc = new XDocument(new XElement("Settings",
                new XElement("Presets",
                    presets.Select(p => new XElement("Preset",
                        new XAttribute("Name", p.Name),
                        new XElement("TrimStart", p.TrimStart),
                        new XElement("TrimEnd", p.TrimEnd),
                        new XElement("CutWidth", p.CutWidth)
                    ))
                )
            ));
            doc.Save(XmlPath);
        }
    }
}
