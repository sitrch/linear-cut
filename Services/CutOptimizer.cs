using System;
using System.Collections.Generic;
using System.Linq;
using LinearCutWpf.Models;

namespace LinearCutWpf.Services
{
    /// <summary>
    /// Предоставляет алгоритмы для оптимизации линейного раскроя хлыстов.
    /// Использует "жадный" алгоритм (First Fit Decreasing) для распределения деталей по хлыстам.
    /// </summary>
    public static class CutOptimizer
    {
        /// <summary>
        /// Базовый метод оптимизации раскроя (упрощенный вариант).
        /// </summary>
        /// <param name="parts">Список длин деталей (с учетом ширины реза).</param>
        /// <param name="stocks">Список доступных длин хлыстов.</param>
        /// <param name="reduction">Суммарный торцевой припуск на хлыст.</param>
        /// <param name="cutWidth">Ширина реза (диска).</param>
        /// <returns>Список сгенерированных раскройных карт (хлыстов с деталями).</returns>
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

        /// <summary>
        /// Расширенный метод оптимизации с учетом конечных и бесконечных остатков.
        /// </summary>
        /// <param name="parts">Список длин деталей.</param>
        /// <param name="infiniteStocks">Список доступных длин целых хлыстов (бесконечное кол-во).</param>
        /// <param name="finiteStocks">Список конечных остатков.</param>
        /// <param name="tStart">Торцевой припуск в начале.</param>
        /// <param name="tEnd">Торцевой припуск в конце.</param>
        /// <param name="cWidth">Ширина реза (диска).</param>
        /// <returns>Список сгенерированных раскройных карт.</returns>
        public static List<CutBar> Optimize(List<double> parts, List<double> infiniteStocks, List<double> finiteStocks, double tStart, double tEnd, double cWidth)
        {
            var results = new List<CutBar>();
            var remaining = parts.OrderByDescending(p => p).ToList();
            double reduction = (tStart - cWidth / 2) + (tEnd - cWidth / 2);
            
            var availableFiniteStocks = finiteStocks?.OrderByDescending(x => x).ToList() ?? new List<double>();

            while (remaining.Any())
            {
                double bestS = 0;
                bool useFinite = false;
                int finiteIdx = -1;
                double capacity = 0;

                // Сначала пытаемся использовать конечные остатки (они уже уменьшены на reduction в CuttingService, но для простоты добавим логику)
                // Остатки передаются сюда как реальная длина остатка. Так как это кусок профиля, ему нужны новые припуски? 
                // В текущей логике мы будем относиться к ним как к обычным хлыстам, чтобы не усложнять, но они конечны.
                for (int i = 0; i < availableFiniteStocks.Count; i++)
                {
                    double s = availableFiniteStocks[i];
                    if (s >= remaining[0]) // Остаток уже чистая вместимость (capacity) без припусков
                    {
                        bestS = s;
                        capacity = s; // capacity равна самому остатку
                        useFinite = true;
                        finiteIdx = i;
                        break;
                    }
                }

                if (!useFinite)
                {
                    bestS = infiniteStocks.OrderByDescending(x => x).FirstOrDefault(s => (s - reduction) >= remaining[0]);
                    if (bestS == 0) { remaining.RemoveAt(0); continue; }
                    capacity = bestS - reduction;
                }
                else
                {
                    availableFiniteStocks.RemoveAt(finiteIdx);
                }

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

        /// <summary>
        /// Перегрузка расширенного метода оптимизации без учета конечных остатков.
        /// </summary>
        public static List<CutBar> Optimize(List<double> parts, List<double> stocks, double tStart, double tEnd, double cWidth)
        {
            return Optimize(parts, stocks, null, tStart, tEnd, cWidth);
        }

        /// <summary>
        /// Детализированная оптимизация раскроя, возвращающая объекты деталей, а не только их длины.
        /// Учитывает конечные остатки.
        /// </summary>
        /// <param name="parts">Список объектов деталей (PartItem).</param>
        /// <param name="infiniteStocks">Список доступных длин целых хлыстов (бесконечное кол-во).</param>
        /// <param name="finiteStocks">Список конечных остатков.</param>
        /// <param name="tStart">Торцевой припуск в начале.</param>
        /// <param name="tEnd">Торцевой припуск в конце.</param>
        /// <param name="cWidth">Ширина реза (диска).</param>
        /// <returns>Список сгенерированных детализированных раскройных карт.</returns>
        public static List<CutBarDetailed> Optimize(List<PartItem> parts, List<double> infiniteStocks, List<double> finiteStocks, double tStart, double tEnd, double cWidth)
        {
            var results = new List<CutBarDetailed>();
            var remaining = parts.OrderByDescending(p => p.Length).ToList();
            double reduction = (tStart - cWidth / 2) + (tEnd - cWidth / 2);
            
            var availableFiniteStocks = finiteStocks?.OrderByDescending(x => x).ToList() ?? new List<double>();

            while (remaining.Any())
            {
                double bestS = 0;
                bool useFinite = false;
                int finiteIdx = -1;
                double capacity = 0;

                for (int i = 0; i < availableFiniteStocks.Count; i++)
                {
                    double s = availableFiniteStocks[i];
                    if (s >= remaining[0].Length)
                    {
                        bestS = s;
                        capacity = s; 
                        useFinite = true;
                        finiteIdx = i;
                        break;
                    }
                }

                if (!useFinite)
                {
                    bestS = infiniteStocks.OrderByDescending(x => x).FirstOrDefault(s => (s - reduction) >= remaining[0].Length);
                    if (bestS == 0) { remaining.RemoveAt(0); continue; }
                    capacity = bestS - reduction;
                }
                else
                {
                    availableFiniteStocks.RemoveAt(finiteIdx);
                }

                var currentBarParts = new List<PartItem>();
                double currentUsed = 0;

                if (remaining[0].Length > capacity / 2)
                {
                    currentBarParts.Add(remaining[0]);
                    currentUsed += remaining[0].Length;
                    remaining.RemoveAt(0);
                }

                for (int i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i].Length <= (capacity - currentUsed))
                    {
                        currentBarParts.Add(remaining[i]);
                        currentUsed += remaining[i].Length;
                        remaining.RemoveAt(i);
                        i--;
                    }
                }

                results.Add(new CutBarDetailed
                {
                    StockLength = bestS,
                    Parts = currentBarParts,
                    Remainder = Math.Round(capacity - currentUsed, 2)
                });
            }
            return results;
        }
    }
}
