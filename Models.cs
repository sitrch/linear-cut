using System.Collections.Generic;
using System.Linq;

namespace LinearCutOptimization
{
    

    // Настройки из XML
    public class CutSettings
    {
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public double CutWidth { get; set; }
        public Dictionary<string, List<double>> ItemStocks { get; set; } = new Dictionary<string, List<double>>();
    }

    public class PresetModel
    {
        public string Name { get; set; }
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public double CutWidth { get; set; }
    }

    // Результат раскроя одного хлыста
    public class CutBar
    {
        public double StockLength { get; set; }
        public string Parts { get; set; }
        public double Remainder { get; set; }
    }

    public class StockModel
    {
        public double Length { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

   
    public class ExportSettings
    {
        public string Параметр { get; set; }
        public string Значение { get; set; }
    }
    public class ManualCutRow
    {
        public double? StockLength { get; set; } // Пусто вместо 6000
        public string Size1 { get; set; } // null вместо "0"
        public string Size2 { get; set; }
        public string Size3 { get; set; }
        public string Size4 { get; set; }

        public double GetRemainder(double tStart, double tEnd, double cWidth)
        {
            double stock = StockLength ?? 0;
            if (stock == 0) return 0;

            double[] sizes = {
            double.TryParse(Size1, out var s1) ? s1 : 0,
            double.TryParse(Size2, out var s2) ? s2 : 0,
            double.TryParse(Size3, out var s3) ? s3 : 0,
            double.TryParse(Size4, out var s4) ? s4 : 0
        };

            double used = sizes.Where(s => s > 0).Sum();
            int cuts = sizes.Count(s => s > 0);
            double red = (tStart - cWidth / 2) + (tEnd - cWidth / 2);

            return stock - red - (used + (cuts * cWidth));
        }
    }

}
