using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearCut
{
    /// <summary>
    /// Класс для оптимизации линейного раскроя профиля
    /// </summary>
    public class LinearCutOptimizer
    {
        /// <summary>
        /// Заготовка для раскроя
        /// </summary>
        public class BlankData
        {
            public string Article { get; set; }
            public string Color { get; set; }
            public double Length { get; set; }
            public int Quantity { get; set; }
        }

        /// <summary>
        /// Профиль и его доступные хлысты
        /// </summary>
        public class ProfileStock
        {
            public string Article { get; set; }
            public double BilletLength { get; set; }
        }

        /// <summary>
        /// Параметры раскроя для профиля
        /// </summary>
        public class CutParameters
        {
            public string Article { get; set; }
            public double TrimStart { get; set; }
            public double TrimEnd { get; set; }
            public double CutWidth { get; set; }
        }

        /// <summary>
        /// Результат раскроя одного хлыста
        /// </summary>
        public class CutResult
        {
            public string Article { get; set; }
            public double BilletLength { get; set; }
            public List<CutPiece> Pieces { get; set; }
            public double Waste { get; set; }
            public double UtilizationPercent { get; set; }
        }

        /// <summary>
        /// Один вырезанный кусок
        /// </summary>
        public class CutPiece
        {
            public string Article { get; set; }
            public string Color { get; set; }
            public double Length { get; set; }
            public int Quantity { get; set; }
        }

        /// <summary>
        /// Остаток материала
        /// </summary>
        public class Remnant
        {
            public string Article { get; set; }
            public double Length { get; set; }
            public DateTime CutDate { get; set; }
        }

        private List<BlankData> _blanks;
        private List<ProfileStock> _stocks;
        private Dictionary<string, CutParameters> _cutParameters;
        private double _minRemnantLength;
        private List<Remnant> _remnants;

        public LinearCutOptimizer(double minRemnantLength = 100)
        {
            _blanks = new List<BlankData>();
            _stocks = new List<ProfileStock>();
            _cutParameters = new Dictionary<string, CutParameters>();
            _remnants = new List<Remnant>();
            _minRemnantLength = minRemnantLength;
        }

        public void AddBlank(BlankData blank)
        {
            _blanks.Add(blank);
        }

        public void AddBlanks(List<BlankData> blanks)
        {
            _blanks.AddRange(blanks);
        }

        public void AddStock(string article, double billetLength)
        {
            _stocks.Add(new ProfileStock { Article = article, BilletLength = billetLength });
        }

        public void SetCutParameters(string article, double trimStart, double trimEnd, double cutWidth)
        {
            _cutParameters[article] = new CutParameters
            {
                Article = article,
                TrimStart = trimStart,
                TrimEnd = trimEnd,
                CutWidth = cutWidth
            };
        }

        public List<CutResult> Optimize()
        {
            var results = new List<CutResult>();
            var remainingBlanks = new Dictionary<string, int>();

            foreach (var blank in _blanks)
            {
                string key = $"{blank.Article}|{blank.Color}|{blank.Length}";
                if (!remainingBlanks.ContainsKey(key))
                    remainingBlanks[key] = 0;
                remainingBlanks[key] += blank.Quantity;
            }

            var blanksByArticle = _blanks.GroupBy(b => b.Article).ToList();

            foreach (var articleGroup in blanksByArticle)
            {
                string article = articleGroup.Key;
                var availableStocks = _stocks
                    .Where(s => s.Article == article)
                    .OrderByDescending(s => s.BilletLength)
                    .ToList();

                if (!availableStocks.Any())
                    continue;

                var cutParams = _cutParameters.ContainsKey(article)
                    ? _cutParameters[article]
                    : new CutParameters { TrimStart = 0, TrimEnd = 0, CutWidth = 0 };

                var articleBlanks = articleGroup.ToList();

                foreach (var stock in availableStocks)
                {
                    while (HasRemainingBlanks(articleBlanks, remainingBlanks))
                    {
                        var cutResult = CutFromBillet(stock, articleBlanks, cutParams, remainingBlanks);
                        
                        if (cutResult.Pieces.Any())
                        {
                            results.Add(cutResult);

                            if (cutResult.Waste >= _minRemnantLength)
                            {
                                _remnants.Add(new Remnant
                                {
                                    Article = article,
                                    Length = cutResult.Waste,
                                    CutDate = DateTime.Now
                                });
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return results;
        }

        private CutResult CutFromBillet(
            ProfileStock stock,
            List<BlankData> articleBlanks,
            CutParameters cutParams,
            Dictionary<string, int> remainingBlanks)
        {
            var result = new CutResult
            {
                Article = stock.Article,
                BilletLength = stock.BilletLength,
                Pieces = new List<CutPiece>()
            };

            double availableLength = stock.BilletLength - cutParams.TrimStart - cutParams.TrimEnd;
            double usedLength = 0;

            var sortedBlanks = articleBlanks
                .OrderByDescending(b => b.Length)
                .ToList();

            foreach (var blank in sortedBlanks)
            {
                string key = $"{blank.Article}|{blank.Color}|{blank.Length}";
                
                if (!remainingBlanks.ContainsKey(key) || remainingBlanks[key] <= 0)
                    continue;

                int canCut = (int)((availableLength - usedLength) / (blank.Length + cutParams.CutWidth));
                
                if (canCut <= 0)
                    continue;

                int toCut = Math.Min(canCut, remainingBlanks[key]);
                
                result.Pieces.Add(new CutPiece
                {
                    Article = blank.Article,
                    Color = blank.Color,
                    Length = blank.Length,
                    Quantity = toCut
                });

                usedLength += toCut * (blank.Length + cutParams.CutWidth) - cutParams.CutWidth;
                remainingBlanks[key] -= toCut;
            }

            result.Waste = availableLength - usedLength;
            result.UtilizationPercent = availableLength > 0 
                ? (usedLength / availableLength) * 100 
                : 0;

            return result;
        }

        private bool HasRemainingBlanks(List<BlankData> articleBlanks, Dictionary<string, int> remainingBlanks)
        {
            return articleBlanks.Any(b =>
            {
                string key = $"{b.Article}|{b.Color}|{b.Length}";
                return remainingBlanks.ContainsKey(key) && remainingBlanks[key] > 0;
            });
        }

        public List<Remnant> GetRemnants()
        {
            return _remnants;
        }

        public void SetMinRemnantLength(double minLength)
        {
            _minRemnantLength = minLength;
        }
    }
}
