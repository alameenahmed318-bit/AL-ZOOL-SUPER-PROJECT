using System;
using System.Collections.Generic;
using System.Linq;

namespace ALZOOOOL
{
    public sealed class Candle
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }

    public enum MarketRegime
    {
        Unknown,
        UpTrend,
        DownTrend,
        Sideways,
        HighVolatility,
        Breakout
    }

    public enum TradeSignal
    {
        Wait,
        Buy,
        Sell
    }

    public sealed class StrategyResult
    {
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
        public TradeSignal Signal { get; set; }
        public string Reason { get; set; } = "";
        public double Strength { get; set; }
    }

    public sealed class MarketAnalysis
    {
        public MarketRegime Regime { get; set; }
        public double LastPrice { get; set; }
        public double EmaFast { get; set; }
        public double EmaSlow { get; set; }
        public double Rsi { get; set; }
        public double Atr { get; set; }
        public double Adx { get; set; }
        public double BollingerUpper { get; set; }
        public double BollingerLower { get; set; }
        public string Description { get; set; } = "";
        public StrategyResult BestStrategy { get; set; } = new();
        public List<StrategyResult> Results { get; set; } = new();
    }

    public static class StrategyEngine
    {
        public static readonly string[] StrategyNames =
        {
            "EMA Trend",
            "EMA Crossover",
            "RSI Reversal",
            "MACD Momentum",
            "Bollinger Reversal",
            "Stochastic Reversal",
            "ADX Trend",
            "ATR Breakout",
            "Donchian Breakout",
            "Support Resistance",
            "Momentum",
            "Fibonacci Pullback",
            "Liquidity Sweep",
            "Multi-Timeframe Trend",
            "Price Action"
        };

        public static readonly string[] CombinationNames =
        {
            "EMA + RSI",
            "EMA + MACD",
            "RSI + Bollinger",
            "MACD + ADX",
            "Breakout + ATR",
            "Support + RSI",
            "Liquidity + Structure",
            "SMC + EMA",
            "ICT + Liquidity",
            "Multi-Timeframe + Momentum",
            "EMA + ADX + RSI",
            "Bollinger + Stochastic",
            "Donchian + Volume",
            "Fibonacci + Price Action",
            "ATR + Trend Filter"
        };

        public static MarketAnalysis Analyze(
            List<Candle> candles,
            string selectedMode = "تلقائي",
            string selectedStrategy = "تلقائي")
        {
            var analysis = new MarketAnalysis();

            if (candles == null || candles.Count < 60)
            {
                analysis.Regime = MarketRegime.Unknown;
                analysis.Description =
                    "بيانات الشموع غير كافية. يلزم 60 شمعة على الأقل.";
                analysis.BestStrategy = WaitResult(
                    "لا توجد بيانات كافية",
                    "النظام",
                    analysis.Description);
                return analysis;
            }

            candles = candles
                .Where(c => c.High >= c.Low &&
                            c.Open > 0 &&
                            c.Close > 0)
                .OrderBy(c => c.Time)
                .ToList();

            if (candles.Count < 60)
            {
                analysis.Description = "بيانات الشموع غير صالحة.";
                analysis.BestStrategy = WaitResult(
                    "انتظار", "النظام", analysis.Description);
                return analysis;
            }

            var closes = candles.Select(c => c.Close).ToList();

            analysis.LastPrice = closes[^1];
            analysis.EmaFast = EMA(closes, 9);
            analysis.EmaSlow = EMA(closes, 21);
            analysis.Rsi = RSI(closes, 14);
            analysis.Atr = ATR(candles, 14);
            analysis.Adx = ADX(candles, 14);

            var bands = Bollinger(closes, 20, 2);
            analysis.BollingerUpper = bands.upper;
            analysis.BollingerLower = bands.lower;

            analysis.Regime = DetectRegime(candles, analysis);
            analysis.Description = DescribeRegime(analysis.Regime);

            analysis.Results = EvaluateStrategies(candles, analysis);

            if (selectedMode == "يدوي" &&
                selectedStrategy != "تلقائي")
            {
                analysis.BestStrategy =
                    analysis.Results.FirstOrDefault(
                        x => x.Name == selectedStrategy)
                    ?? WaitResult(
                        "استراتيجية غير متاحة",
                        "النظام",
                        "لم يتم العثور على الاستراتيجية المطلوبة.");
            }
            else
            {
                analysis.BestStrategy =
                    SelectAutomatic(analysis);
            }

            return analysis;
        }

        private static MarketRegime DetectRegime(
            List<Candle> candles,
            MarketAnalysis a)
        {
            double averageRange = candles
                .TakeLast(20)
                .Average(c => c.High - c.Low);

            double recentRange = candles
                .TakeLast(5)
                .Average(c => c.High - c.Low);

            double previousHigh = candles
                .Skip(candles.Count - 21)
                .Take(20)
                .Max(c => c.High);

            double previousLow = candles
                .Skip(candles.Count - 21)
                .Take(20)
                .Min(c => c.Low);

            double price = a.LastPrice;

            if (recentRange > averageRange * 1.8)
                return MarketRegime.HighVolatility;

            if (price > previousHigh || price < previousLow)
                return MarketRegime.Breakout;

            if (a.Adx >= 23 && a.EmaFast > a.EmaSlow)
                return MarketRegime.UpTrend;

            if (a.Adx >= 23 && a.EmaFast < a.EmaSlow)
                return MarketRegime.DownTrend;

            return MarketRegime.Sideways;
        }

        private static string DescribeRegime(MarketRegime regime)
        {
            return regime switch
            {
                MarketRegime.UpTrend =>
                    "السوق في اتجاه صاعد.",
                MarketRegime.DownTrend =>
                    "السوق في اتجاه هابط.",
                MarketRegime.Sideways =>
                    "السوق يتحرك بشكل عرضي.",
                MarketRegime.HighVolatility =>
                    "تقلبات السوق مرتفعة. يلزم الحذر.",
                MarketRegime.Breakout =>
                    "تم رصد حركة اختراق محتملة.",
                _ => "حالة السوق غير معروفة."
            };
        }

        private static List<StrategyResult> EvaluateStrategies(
            List<Candle> candles,
            MarketAnalysis a)
        {
            var results = new List<StrategyResult>();
            double price = a.LastPrice;
            double previous = candles[^2].Close;

            TradeSignal trend =
                a.EmaFast > a.EmaSlow
                    ? TradeSignal.Buy
                    : a.EmaFast < a.EmaSlow
                        ? TradeSignal.Sell
                        : TradeSignal.Wait;

            TradeSignal rsiSignal =
                a.Rsi < 30 ? TradeSignal.Buy :
                a.Rsi > 70 ? TradeSignal.Sell :
                TradeSignal.Wait;

            var macd = MACD(candles.Select(c => c.Close).ToList());

            TradeSignal macdSignal =
                macd.macd > macd.signal
                    ? TradeSignal.Buy
                    : TradeSignal.Sell;

            var bands = Bollinger(
                candles.Select(c => c.Close).ToList(), 20, 2);

            TradeSignal bollingerSignal =
                price <= bands.lower ? TradeSignal.Buy :
                price >= bands.upper ? TradeSignal.Sell :
                TradeSignal.Wait;

            double previousHigh = candles
                .Skip(candles.Count - 21)
                .Take(20)
                .Max(c => c.High);

            double previousLow = candles
                .Skip(candles.Count - 21)
                .Take(20)
                .Min(c => c.Low);

            TradeSignal breakout =
                price > previousHigh ? TradeSignal.Buy :
                price < previousLow ? TradeSignal.Sell :
                TradeSignal.Wait;

            Add(results, "EMA Trend", "الاتجاه",
                trend, "اتجاه المتوسطات المتحركة.", 65);

            Add(results, "EMA Crossover", "الاتجاه",
                trend, "تقاطع EMA 9 و EMA 21.", 65);

            Add(results, "RSI Reversal", "الانعكاس",
                rsiSignal, $"قيمة RSI: {a.Rsi:F1}", 60);

            Add(results, "MACD Momentum", "الزخم",
                macdSignal, "مقارنة MACD وخط الإشارة.", 60);

            Add(results, "Bollinger Reversal", "التذبذب",
                bollingerSignal, "موقع السعر بالنسبة للحدود.", 55);

            Add(results, "Stochastic Reversal", "الانعكاس",
                StochasticSignal(candles),
                "مؤشر Stochastic محسوب من آخر الشموع.", 55);

            Add(results, "ADX Trend", "الاتجاه",
                a.Adx >= 23 ? trend : TradeSignal.Wait,
                $"قوة الاتجاه ADX: {a.Adx:F1}", 65);

            Add(results, "ATR Breakout", "الاختراق",
                a.Adx >= 20 ? breakout : TradeSignal.Wait,
                $"ATR: {a.Atr:F3}", 60);

            Add(results, "Donchian Breakout", "الاختراق",
                breakout, "اختراق أعلى أو أدنى آخر 20 شمعة.", 60);

            Add(results, "Support Resistance", "الدعم والمقاومة",
                SupportResistanceSignal(candles),
                "مقارنة السعر بمناطق الدعم والمقاومة القريبة.", 55);

            Add(results, "Momentum", "الزخم",
                price > previous ? TradeSignal.Buy :
                price < previous ? TradeSignal.Sell :
                TradeSignal.Wait,
                "اتجاه حركة آخر شمعة.", 50);

            Add(results, "Fibonacci Pullback", "فيبوناتشي",
                FibonacciSignal(candles),
                "فحص ارتداد السعر من منطقة تصحيح تقريبية.", 50);

            Add(results, "Liquidity Sweep", "السيولة",
                LiquiditySweepSignal(candles),
                "فحص تجاوز القمة أو القاع ثم الإغلاق داخله.", 55);

            Add(results, "Multi-Timeframe Trend", "متعدد الأطر",
                trend,
                "اتجاه الإطار الحالي فقط؛ يلزم ربط أطر إضافية للتأكيد.",
                50);

            Add(results, "Price Action", "حركة السعر",
                PriceActionSignal(candles),
                "فحص نماذج الشموع الأساسية.", 55);

            AddCombination(results, "EMA + RSI",
                Combine(trend, rsiSignal),
                "اتفاق الاتجاه مع RSI.");

            AddCombination(results, "EMA + MACD",
                Combine(trend, macdSignal),
                "اتفاق EMA مع MACD.");

            AddCombination(results, "RSI + Bollinger",
                Combine(rsiSignal, bollingerSignal),
                "تأكيد الانعكاس بواسطة RSI والبولنجر.");

            AddCombination(results, "MACD + ADX",
                a.Adx >= 23 ? macdSignal : TradeSignal.Wait,
                "زخم MACD مع قوة الاتجاه ADX.");

            AddCombination(results, "Breakout + ATR",
                a.Atr > 0 ? breakout : TradeSignal.Wait,
                "اختراق مع قياس التقلب.");

            AddCombination(results, "Support + RSI",
                Combine(
                    SupportResistanceSignal(candles),
                    rsiSignal),
                "مناطق الدعم والمقاومة مع RSI.");

            AddCombination(results, "Liquidity + Structure",
                Combine(
                    LiquiditySweepSignal(candles),
                    PriceActionSignal(candles)),
                "السيولة مع حركة السعر.");

            AddCombination(results, "SMC + EMA",
                trend,
                "اتجاه EMA فقط؛ تحليل SMC الكامل غير مطبق بعد.");

            AddCombination(results, "ICT + Liquidity",
                LiquiditySweepSignal(candles),
                "فحص السيولة فقط؛ نموذج ICT الكامل غير مطبق بعد.");

            AddCombination(results, "Multi-Timeframe + Momentum",
                Combine(trend,
                    price > previous
                        ? TradeSignal.Buy
                        : TradeSignal.Sell),
                "اتجاه وزخم الإطار الحالي.");

            AddCombination(results, "EMA + ADX + RSI",
                a.Adx >= 23
                    ? Combine(trend, rsiSignal)
                    : TradeSignal.Wait,
                "تأكيد الاتجاه والقوة وRSI.");

            AddCombination(results, "Bollinger + Stochastic",
                Combine(
                    bollingerSignal,
                    StochasticSignal(candles)),
                "تأكيد الانعكاس بمؤشرين.");

            AddCombination(results, "Donchian + Volume",
                breakout,
                "اختراق القناة؛ يلزم توافر بيانات حجم موثوقة.");

            AddCombination(results, "Fibonacci + Price Action",
                Combine(
                    FibonacciSignal(candles),
                    PriceActionSignal(candles)),
                "تصحيح سعري مع نموذج شموع.");

            AddCombination(results, "ATR + Trend Filter",
                a.Atr > 0 ? trend : TradeSignal.Wait,
                "فلتر اتجاه مع قياس التقلب.");

            return results;
        }

        private static StrategyResult SelectAutomatic(MarketAnalysis a)
        {
            IEnumerable<StrategyResult> candidates =
                a.Results.Where(x => x.Signal != TradeSignal.Wait);

            candidates = a.Regime switch
            {
                MarketRegime.UpTrend =>
                    candidates.Where(x =>
                        x.Name.Contains("EMA") ||
                        x.Name.Contains("MACD") ||
                        x.Name.Contains("ADX") ||
                        x.Name.Contains("Momentum")),

                MarketRegime.DownTrend =>
                    candidates.Where(x =>
                        x.Name.Contains("EMA") ||
                        x.Name.Contains("MACD") ||
                        x.Name.Contains("ADX") ||
                        x.Name.Contains("Momentum")),

                MarketRegime.Sideways =>
                    candidates.Where(x =>
                        x.Name.Contains("RSI") ||
                        x.Name.Contains("Bollinger") ||
                        x.Name.Contains("Stochastic") ||
                        x.Name.Contains("Support")),

                MarketRegime.Breakout =>
                    candidates.Where(x =>
                        x.Name.Contains("Breakout") ||
                        x.Name.Contains("Donchian") ||
                        x.Name.Contains("ATR")),

                MarketRegime.HighVolatility =>
                    candidates.Where(x =>
                        x.Name.Contains("ATR") ||
                        x.Name.Contains("ADX")),

                _ => Enumerable.Empty<StrategyResult>()
            };

            var best = candidates
                .OrderByDescending(x => x.Strength)
                .FirstOrDefault();

            return best ?? WaitResult(
                "انتظار",
                "الاختيار التلقائي",
                "لا توجد إشارة مناسبة وواضحة لحالة السوق الحالية.");
        }

        private static void Add(
            List<StrategyResult> list,
            string name,
            string group,
            TradeSignal signal,
            string reason,
            double strength)
        {
            list.Add(new StrategyResult
            {
                Name = name,
                Group = group,
                Signal = signal,
                Reason = reason,
                Strength = signal == TradeSignal.Wait ? 0 : strength
            });
        }

        private static void AddCombination(
            List<StrategyResult> list,
            string name,
            TradeSignal signal,
            string reason)
        {
            Add(list, name, "تركيبة", signal, reason, 70);
        }

        private static TradeSignal Combine(
            TradeSignal a,
            TradeSignal b)
        {
            return a != TradeSignal.Wait && a == b
                ? a
                : TradeSignal.Wait;
        }

        private static StrategyResult WaitResult(
            string name, string group, string reason)
        {
            return new StrategyResult
            {
                Name = name,
                Group = group,
                Signal = TradeSignal.Wait,
                Reason = reason,
                Strength = 0
            };
        }

        private static double EMA(List<double> values, int period)
        {
            double multiplier = 2.0 / (period + 1);
            double ema = values.Take(period).Average();

            for (int i = period; i < values.Count; i++)
                ema = (values[i] - ema) * multiplier + ema;

            return ema;
        }

        private static double RSI(List<double> values, int period)
        {
            double gains = 0;
            double losses = 0;

            for (int i = values.Count - period; i < values.Count; i++)
            {
                double change = values[i] - values[i - 1];

                if (change > 0) gains += change;
                else losses -= change;
            }

            if (losses == 0) return 100;

            double rs = (gains / period) / (losses / period);
            return 100 - (100 / (1 + rs));
        }

        private static double ATR(List<Candle> candles, int period)
        {
            var ranges = new List<double>();

            for (int i = candles.Count - period; i < candles.Count; i++)
            {
                var c = candles[i];
                double previousClose = candles[i - 1].Close;

                ranges.Add(Math.Max(
                    c.High - c.Low,
                    Math.Max(
                        Math.Abs(c.High - previousClose),
                        Math.Abs(c.Low - previousClose))));
            }

            return ranges.Average();
        }

        private static double ADX(List<Candle> candles, int period)
        {
            double plus = 0;
            double minus = 0;
            double tr = 0;

            for (int i = candles.Count - period; i < candles.Count; i++)
            {
                var current = candles[i];
                var previous = candles[i - 1];

                double up = current.High - previous.High;
                double down = previous.Low - current.Low;

                if (up > down && up > 0) plus += up;
                if (down > up && down > 0) minus += down;

                tr += Math.Max(
                    current.High - current.Low,
                    Math.Max(
                        Math.Abs(current.High - previous.Close),
                        Math.Abs(current.Low - previous.Close)));
            }

            if (tr == 0 || plus + minus == 0) return 0;

            double plusDI = 100 * plus / tr;
            double minusDI = 100 * minus / tr;

            return 100 * Math.Abs(plusDI - minusDI) /
                   (plusDI + minusDI);
        }

        private static (double upper, double lower) Bollinger(
            List<double> values, int period, double deviation)
        {
            var sample = values.TakeLast(period).ToList();
            double average = sample.Average();

            double variance = sample.Average(
                x => Math.Pow(x - average, 2));

            double sd = Math.Sqrt(variance);

            return (
                average + deviation * sd,
                average - deviation * sd
            );
        }

        private static (double macd, double signal) MACD(
            List<double> values)
        {
            var macdValues = new List<double>();

            for (int i = 25; i < values.Count; i++)
            {
                var sample = values.Take(i + 1).ToList();
                macdValues.Add(
                    EMA(sample, 12) - EMA(sample, 26));
            }

            double macd = macdValues[^1];
            double signal = EMA(macdValues, 9);

            return (macd, signal);
        }

        private static TradeSignal StochasticSignal(
            List<Candle> candles)
        {
            var sample = candles.TakeLast(14).ToList();
            double high = sample.Max(x => x.High);
            double low = sample.Min(x => x.Low);

            if (high == low) return TradeSignal.Wait;

            double k = 100 *
                (sample[^1].Close - low) / (high - low);

            if (k < 20) return TradeSignal.Buy;
            if (k > 80) return TradeSignal.Sell;

            return TradeSignal.Wait;
        }

        private static TradeSignal SupportResistanceSignal(
            List<Candle> candles)
        {
            var sample = candles.TakeLast(30).ToList();
            double high = sample.Max(x => x.High);
            double low = sample.Min(x => x.Low);
            double price = sample[^1].Close;
            double range = high - low;

            if (range <= 0) return TradeSignal.Wait;

            if (price <= low + range * 0.10)
                return TradeSignal.Buy;

            if (price >= high - range * 0.10)
                return TradeSignal.Sell;

            return TradeSignal.Wait;
        }

        private static TradeSignal FibonacciSignal(
            List<Candle> candles)
        {
            var sample = candles.TakeLast(30).ToList();
            double high = sample.Max(x => x.High);
            double low = sample.Min(x => x.Low);
            double price = sample[^1].Close;
            double range = high - low;

            if (range <= 0) return TradeSignal.Wait;

            double level38 = high - range * 0.382;
            double level62 = high - range * 0.618;

            if (price >= level62 && price <= level38)
            {
                return sample[^1].Close >= sample[^1].Open
                    ? TradeSignal.Buy
                    : TradeSignal.Sell;
            }

            return TradeSignal.Wait;
        }

        private static TradeSignal LiquiditySweepSignal(
            List<Candle> candles)
        {
            var previous = candles
                .Skip(candles.Count - 21)
                .Take(20)
                .ToList();

            var current = candles[^1];

            double high = previous.Max(x => x.High);
            double low = previous.Min(x => x.Low);

            if (current.Low < low && current.Close > low)
                return TradeSignal.Buy;

            if (current.High > high && current.Close < high)
                return TradeSignal.Sell;

            return TradeSignal.Wait;
        }

        private static TradeSignal PriceActionSignal(
            List<Candle> candles)
        {
            var c = candles[^1];

            double body = Math.Abs(c.Close - c.Open);
            double upperWick = c.High - Math.Max(c.Open, c.Close);
            double lowerWick = Math.Min(c.Open, c.Close) - c.Low;

            if (lowerWick > body * 2 && c.Close > c.Open)
                return TradeSignal.Buy;

            if (upperWick > body * 2 && c.Close < c.Open)
                return TradeSignal.Sell;

            return TradeSignal.Wait;
        }
    }
}