using System;
using System.Collections.Generic;
using System.Linq;
using LinearCutWpf.Models;

namespace LinearCutWpf.Services
{
    public static class CutOptimizer
    {
        public static List<CutBar> Optimize(List<double> parts, List<double> stocks, double reduction, double cutWidth)
        {
            var results = new List<CutBar>();
            var remainingParts = parts.OrderByDescending(p => p).ToList();

            while (remainingParts.Any())
            {
                double bestStock = stocks.OrderByDescending(x => x).FirstOrDefault(s => (s - reduction) >= remainingParts[0]);
                if (bestStock == 0) { remainingParts.RemoveAt(0); continue; }

                double capacity = bestStock - reduction;
                var currentBarParts = new List<double>();

                if (remainingParts[0] > capacity / 2)
                {
                    currentBarParts.Add(remainingParts[0]);
                    remainingParts.RemoveAt(0);
                }

                for (int i = 0; i < remainingParts.Count; i++)
                {
                    if (remainingParts[i] <= (capacity - currentBarParts.Sum()))
                    {
                        currentBarParts.Add(remainingParts[i]);
                        remainingParts.RemoveAt(i);
                        i--;
                    }
                }

                results.Add(new CutBar
                {
                    StockLength = bestStock,
                    Parts = string.Join(" + ", currentBarParts.Select(p => p - cutWidth)),
                    Remainder = Math.Round(capacity - currentBarParts.Sum(), 2)
                });
            }
            return results;
        }

        public static List<CutBar> Optimize(List<double> parts, List<double> stocks, double tStart, double tEnd, double cWidth)
        {
            var results = new List<CutBar>();
            var remaining = parts.OrderByDescending(p => p).ToList();
            double reduction = (tStart - cWidth / 2) + (tEnd - cWidth / 2);

            while (remaining.Any())
            {
                double bestS = stocks.OrderByDescending(x => x).FirstOrDefault(s => (s - reduction) >= remaining[0]);
                if (bestS == 0) { remaining.RemoveAt(0); continue; }

                double capacity = bestS - reduction;
                var currentBarParts = new List<double>();

                if (remaining[0] > capacity / 2)
                {
                    currentBarParts.Add(remaining[0]);
                    remaining.RemoveAt(0);
                }

                for (int i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i] <= (capacity - currentBarParts.Sum()))
                    {
                        currentBarParts.Add(remaining[i]);
                        remaining.RemoveAt(i);
                        i--;
                    }
                }

                results.Add(new CutBar
                {
                    StockLength = bestS,
                    Parts = string.Join(" + ", currentBarParts.Select(p => p - cWidth)),
                    Remainder = Math.Round(capacity - currentBarParts.Sum(), 2)
                });
            }
            return results;
        }
    }
}