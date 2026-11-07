using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ALZOOOOL
{
    public partial class MainWindow : Window
    {
        private readonly TradingControl tradingControl = new();
        private readonly DispatcherTimer bridgeTimer = new();

        private readonly string bridgePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MetaQuotes", "Terminal", "Common",
            "Files", "ALZOOL", "status.csv");

        private readonly string candlesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MetaQuotes", "Terminal", "Common", "Files", "ALZOOL", "candles.csv");

        private readonly string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ALZOOOL", "settings.json");

        private readonly Dictionary<string, TextBox> inputs = new();
        private readonly Dictionary<string, CheckBox> toggles = new();
        private readonly Dictionary<string, ComboBox> modes = new();

        private SettingsDocument settings;
        private bool mt5Connected;
        private MarketAnalysis? latestAnalysis;

        private ComboBox strategyModeCombo;
        private ComboBox strategyCombo;
        private TextBlock strategyInfo;
        private TextBlock strategyStatus;

        private readonly string[] strategies =
        {
            "تلقائي حسب السوق",
            "EMA Trend",
            "RSI Reversal",
            "MACD Momentum",
            "Bollinger Bands",
            "Stochastic",
            "Support & Resistance",
            "Breakout",
            "Price Action",
            "Fibonacci",
            "Liquidity Sweep",
            "ATR Volatility",
            "ADX Trend",
            "SMC + EMA",
            "ICT + Liquidity",
            "Multi-Timeframe Trend"
        };

        private readonly string[] combinations =
        {
            "تلقائي",
            "EMA + RSI",
            "EMA + MACD",
            "RSI + Bollinger",
            "EMA + ADX",
            "MACD + Stochastic",
            "Support + Resistance",
            "Breakout + Volume",
            "Price Action + EMA",
            "Fibonacci + RSI",
            "Liquidity Sweep + EMA",
            "SMC + EMA",
            "ICT + Liquidity",
            "ATR + Momentum",
            "Multi-Strategy Hybrid"
        };

        public class SettingsDocument
        {
            public Dictionary<string, string> Values { get; set; } = new();
            public Dictionary<string, bool> Enabled { get; set; } = new();
            public Dictionary<string, string> Modes { get; set; } = new();
        }

        public MainWindow()
        {
            InitializeComponent();

            settings = LoadSettings();

            tradingControl.TradingStateChanged += isRunning =>
            {
                Dispatcher.Invoke(() =>
                {
                    TradingStatus.Text = isRunning ? "يعمل" : "متوقف";
                    TradingStatus.Foreground = isRunning
                        ? Brushes.LightGreen : Brushes.OrangeRed;

                    StartButton.IsEnabled = !isRunning && mt5Connected;
                    StopButton.IsEnabled = isRunning;
                });
            };

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = false;

            bridgeTimer.Interval = TimeSpan.FromSeconds(1);
            bridgeTimer.Tick += async (s, e) =>
                await RefreshBridgeAsync(false);

            bridgeTimer.Start();
        }

        private SettingsDocument LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    return JsonSerializer.Deserialize<SettingsDocument>(json)
                           ?? new SettingsDocument();
                }
            }
            catch { }

            return new SettingsDocument();
        }

        private void AddHeading(string title)
        {
            PageContent.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(214, 180, 90)),
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 18, 0, 10)
            });
        }

        private void AddNumericSetting(string key, string label)
        {
            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 40, 36)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(160)
            });

            var check = new CheckBox
            {
                Content = label,
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = settings.Enabled.TryGetValue(key, out bool enabled)
                    && enabled
            };

            var input = new TextBox
            {
                Text = settings.Values.TryGetValue(key, out string? value)
                    ? value : "",
                Height = 32,
                Padding = new Thickness(7),
                Background = new SolidColorBrush(Color.FromRgb(232, 227, 214)),
                Foreground = Brushes.Black,
                VerticalContentAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            Grid.SetColumn(check, 0);
            Grid.SetColumn(input, 1);

            grid.Children.Add(check);
            grid.Children.Add(input);
            row.Child = grid;

            PageContent.Children.Add(row);

            toggles[key] = check;
            inputs[key] = input;
        }

        private void AddBooleanSetting(string key, string label)
        {
            var check = new CheckBox
            {
                Content = label,
                Foreground = Brushes.White,
                FontSize = 15,
                Margin = new Thickness(5, 10, 5, 10),
                IsChecked = settings.Enabled.TryGetValue(key, out bool enabled)
                    && enabled
            };

            PageContent.Children.Add(check);
            toggles[key] = check;
        }

        private void AddModeSetting(string key, string label)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(170)
            });

            var title = new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };

            var combo = new ComboBox
            {
                Height = 32,
                ItemsSource = new[] { "حسابي", "مضاعف" },
                SelectedItem = settings.Modes.TryGetValue(key, out string? mode)
                    ? mode : "حسابي"
            };

            Grid.SetColumn(title, 0);
            Grid.SetColumn(combo, 1);

            row.Children.Add(title);
            row.Children.Add(combo);
            PageContent.Children.Add(row);

            modes[key] = combo;
        }

        private void BuildSettingsPage()
        {
            inputs.Clear();
            toggles.Clear();
            modes.Clear();
            PageContent.Children.Clear();

            PageTitle.Text = "الإعدادات الثابتة والتداول";
            PageDescription.Text =
                "حدد إعدادات التداول بنفسك. لا يتم تشغيل أي إعداد تلقائياً.";

            AddHeading("أولاً: حجم التداول");
            AddNumericSetting("baseLot", "تفعيل حجم اللوت الأساسي");
            AddNumericSetting("maxLot", "تفعيل أقصى حجم لوت مسموح");

            AddHeading("ثانياً: التعزيز");
            AddBooleanSetting("addEnabled", "السماح بصفقات التعزيز");
            AddNumericSetting("addCount", "تفعيل أقصى عدد صفقات التعزيز");
            AddNumericSetting("addDistance", "تفعيل مسافة التعزيز بالنقاط");
            AddNumericSetting("addStep", "تفعيل قيمة زيادة لوت التعزيز");
            AddNumericSetting("addMaxLot", "تفعيل أقصى لوت للتعزيز");
            AddModeSetting("addMode", "طريقة زيادة لوت التعزيز");

            AddHeading("ثالثاً: التبريد");
            AddBooleanSetting("coolEnabled", "السماح بصفقات التبريد");
            AddNumericSetting("coolCount", "تفعيل أقصى عدد صفقات التبريد");
            AddNumericSetting("coolDistance", "تفعيل مسافة التبريد بالنقاط");
            AddNumericSetting("coolStep", "تفعيل قيمة زيادة لوت التبريد");
            AddNumericSetting("coolMaxLot", "تفعيل أقصى لوت للتبريد");
            AddModeSetting("coolMode", "طريقة زيادة لوت التبريد");

            AddHeading("رابعاً: حماية رأس المال");
            AddNumericSetting("dailyLoss", "تفعيل حد الخسارة اليومية بالدرهم");
            AddNumericSetting("floatingLoss", "تفعيل حد خسارة الصفقات المفتوحة بالدرهم");

            AddHeading("خامساً: جني الأرباح");
            AddNumericSetting("singleProfit", "تفعيل هدف ربح الصفقة الواحدة بالدرهم");
            AddNumericSetting("basketProfit", "تفعيل هدف ربح مجموعة الصفقات بالدرهم");
            AddNumericSetting("profitTrigger", "تفعيل بداية حماية الأرباح بالدرهم");
            AddNumericSetting("profitLock", "تفعيل أقل ربح محفوظ بالدرهم");

            AddHeading("سادساً: اتجاه التداول");
            AddBooleanSetting("allowHedging", "السماح بصفقات الشراء والبيع معاً");

            var saveButton = new Button
            {
                Content = "حفظ جميع الإعدادات",
                Height = 48,
                Margin = new Thickness(0, 25, 0, 15),
                Background = new SolidColorBrush(Color.FromRgb(23, 107, 69)),
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            saveButton.Click += SaveSettings_Click;
            PageContent.Children.Add(saveButton);

            PageContent.Children.Add(new TextBlock
            {
                Text = "الإعدادات المحفوظة لا تُطبق على التداول الفعلي حتى ربطها بمحرك التداول والتحقق من حدود الوسيط.",
                Foreground = new SolidColorBrush(Color.FromRgb(214, 180, 90)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 15)
            });
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in inputs)
            {
                bool enabled = toggles[item.Key].IsChecked == true;
                string value = item.Value.Text.Trim();

                if (enabled)
                {
                    bool valid = decimal.TryParse(
                        value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out decimal number) && number > 0;

                    if (!valid)
                    {
                        MessageBox.Show(
                            "أدخل قيمة رقمية أكبر من صفر في: " + item.Key,
                            "قيمة غير صحيحة",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        item.Value.Focus();
                        return;
                    }

                    if ((item.Key == "addCount" || item.Key == "coolCount")
                        && !int.TryParse(value, out _))
                    {
                        MessageBox.Show("عدد الصفقات يجب أن يكون رقماً صحيحاً.");
                        item.Value.Focus();
                        return;
                    }
                }

                settings.Enabled[item.Key] = enabled;
                settings.Values[item.Key] = value;
            }

            foreach (var item in toggles)
                settings.Enabled[item.Key] = item.Value.IsChecked == true;

            foreach (var item in modes)
                settings.Modes[item.Key] =
                    item.Value.SelectedItem?.ToString() ?? "حسابي";

            try
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(settingsPath)!);

                string json = JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(settingsPath, json);

                MessageBox.Show(
                    "تم حفظ الإعدادات بنجاح.",
                    "AL-ZOOL SUPER AI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذر حفظ الإعدادات: " + ex.Message);
            }
        }

        // صفحة مكتبة الاستراتيجيات
        private void BuildStrategyPage()
        {
            PageContent.Children.Clear();

            PageTitle.Text = "مكتبة الاستراتيجيات";
            PageDescription.Text =
                "اختر استراتيجية يدوياً أو اترك النظام يختار حسب السوق.";

            AddHeading("طريقة الاختيار");

            strategyModeCombo = new ComboBox
            {
                Height = 38,
                FontSize = 15,
                ItemsSource = new[] { "اختيار تلقائي", "اختيار يدوي" },
                SelectedIndex = 0,
                Margin = new Thickness(0, 5, 0, 12)
            };

            strategyModeCombo.SelectionChanged += (s, e) =>
            {
                if (strategyCombo != null)
                    strategyCombo.IsEnabled =
                        strategyModeCombo.SelectedIndex == 1;

                UpdateStrategyInfo();
            };

            PageContent.Children.Add(strategyModeCombo);

            AddHeading("الاستراتيجية");

            strategyCombo = new ComboBox
            {
                Height = 38,
                FontSize = 15,
                ItemsSource = strategies,
                SelectedIndex = 0,
                IsEnabled = false,
                Margin = new Thickness(0, 5, 0, 12)
            };

            strategyCombo.SelectionChanged += (s, e) =>
                UpdateStrategyInfo();

            PageContent.Children.Add(strategyCombo);

            AddHeading("تركيبة الاستراتيجيات");

            var combinationCombo = new ComboBox
            {
                Height = 38,
                FontSize = 15,
                ItemsSource = combinations,
                SelectedIndex = 0,
                Margin = new Thickness(0, 5, 0, 12)
            };

            PageContent.Children.Add(combinationCombo);

            AddHeading("حالة محرك التحليل");

            strategyStatus = new TextBlock
            {
                Text = "المحرك جاهز للاختيار - بانتظار بيانات الشموع",
                Foreground = Brushes.LightGreen,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 12)
            };

            PageContent.Children.Add(strategyStatus);

            strategyInfo = new TextBlock
            {
                Text = "",
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 15)
            };

            PageContent.Children.Add(strategyInfo);

            var saveButton = new Button
            {
                Content = "حفظ اختيار الاستراتيجية",
                Height = 45,
                Background = new SolidColorBrush(Color.FromRgb(23, 107, 69)),
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 10)
            };

            saveButton.Click += (s, e) =>
            {
                settings.Modes["strategyMode"] =
                    strategyModeCombo.SelectedItem?.ToString() ?? "اختيار تلقائي";

                settings.Modes["strategy"] =
                    strategyCombo.SelectedItem?.ToString() ?? strategies[0];

                settings.Modes["combination"] =
                    combinationCombo.SelectedItem?.ToString() ?? combinations[0];

                try
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(settingsPath)!);

                    File.WriteAllText(
                        settingsPath,
                        JsonSerializer.Serialize(
                            settings,
                            new JsonSerializerOptions { WriteIndented = true }));

                    MessageBox.Show("تم حفظ اختيار الاستراتيجية.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("تعذر الحفظ: " + ex.Message);
                }
            };

            PageContent.Children.Add(saveButton);

            UpdateStrategyInfo();
        }

        private void UpdateStrategyInfo()
        {
            if (strategyInfo == null || strategyCombo == null ||
                strategyModeCombo == null)
                return;

            if (strategyModeCombo.SelectedIndex == 0)
            {
                strategyInfo.Text =
                    "سيتم اختيار الاستراتيجية المناسبة بعد تحليل حالة السوق " +
                    "وتوفر بيانات الشموع. الاختيار التلقائي لا يفتح صفقات.";
            }
            else
            {
                strategyInfo.Text =
                    "الاستراتيجية المختارة: " +
                    (strategyCombo.SelectedItem?.ToString() ?? "غير محددة") +
                    "\nسيتم استخدام الاختيار اليدوي عند ربط بيانات السوق بالمحرك.";
            }
        }

        private void Navigate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not string section)
                return;

            PageTitle.Text = section;
            PageDescription.Text = "أنت الآن في قسم: " + section;
            PageContent.Children.Clear();

            if (section == "الإعدادات الثابتة" ||
                section == "اللوت والتعزيز والتبريد")
            {
                BuildSettingsPage();
            }
            else if (section == "مكتبة الاستراتيجيات" ||
                     section == "مختبر الاستراتيجيات والتركيبات")
            {
                BuildStrategyPage();
            }
            else
            {
                BuildSectionPage(section);
            }
        }

        // كل الأقسام لها صفحة فعلية. الوظائف التي تحتاج بيانات وسيط أو خدمة
        // خارجية تعرض حالتها بوضوح ولا تعرض أرقاماً أو قرارات مختلقة.
        private void BuildSectionPage(string section)
        {
            PageContent.Children.Clear();
            PageTitle.Text = section;
            PageDescription.Text = GetSectionDescription(section);

            AddHeading("حالة القسم");
            var status = new TextBlock
            {
                Text = GetSectionStatus(section),
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            };
            PageContent.Children.Add(status);

            if (section == "مراقبة النظام والاتصال" || section == "الرئيسية" ||
                section == "مركز التحكم الرئيسي" || section == "الصفقات المفتوحة")
            {
                var refresh = new Button
                {
                    Content = "تحديث بيانات MT5 الآن",
                    Height = 42,
                    Margin = new Thickness(0, 8, 0, 12),
                    Background = new SolidColorBrush(Color.FromRgb(23, 107, 69)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0)
                };
                refresh.Click += async (s, e) => await RefreshBridgeAsync(true);
                PageContent.Children.Add(refresh);
            }

            AddHeading("معلومات وتشغيل القسم");
            PageContent.Children.Add(new TextBlock
            {
                Text = GetSectionDetails(section),
                Foreground = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 14)
            });

            AddHeading("ملاحظاتك لهذا القسم");
            var notes = new TextBox
            {
                Text = settings.Values.TryGetValue("notes_" + section, out string? saved) ? saved : "",
                Height = 90,
                MinWidth = 260,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromRgb(232, 227, 214)),
                Foreground = Brushes.Black
            };
            PageContent.Children.Add(notes);

            var save = new Button
            {
                Content = "حفظ ملاحظات القسم",
                Height = 42,
                Margin = new Thickness(0, 10, 0, 15),
                Background = new SolidColorBrush(Color.FromRgb(23, 107, 69)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            save.Click += (s, e) =>
            {
                settings.Values["notes_" + section] = notes.Text;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                    File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings,
                        new JsonSerializerOptions { WriteIndented = true }));
                    MessageBox.Show("تم حفظ ملاحظات هذا القسم.", "AL-ZOOL SUPER AI");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("تعذر الحفظ: " + ex.Message);
                }
            };
            PageContent.Children.Add(save);
        }

        private string GetSectionDescription(string section) => section switch
        {
            "الرئيسية" => "ملخص حالة التطبيق والاتصال بحساب MT5.",
            "عقل الذكاء الاصطناعي" => "مركز حالة وحدات التحليل وإدارة الصفقات وحماية رأس المال.",
            "تحليل السوق" => "يعرض حالة توفر بيانات الذهب. لا يصدر توصية دون بيانات سوق فعلية.",
            "مختبر الاستراتيجيات والتركيبات" => "إعدادات الاستراتيجيات المتاحة حالياً. الاختبار التاريخي غير مفعّل بعد.",
            "قرارات الذكاء الاصطناعي" => "سجل قرارات التحليل؛ لا توجد قرارات مولدة دون تغذية سوق فعلية.",
            "الصفقات المفتوحة" => "مراقبة اتصال MT5 وعدد الصفقات المبلغ عنه من الجسر.",
            "إدارة المخاطر" => "إعدادات المخاطر المحفوظة محلياً. لم يتم ربطها بتنفيذ أوامر الوسيط.",
            "الأداء والإحصائيات" => "لا تعرض إحصائيات تداول إلا بعد استيراد سجل صفقات حقيقي.",
            "ذاكرة الذكاء الاصطناعي" => "ملاحظات محلية مرتبطة بالقسم محفوظة على هذا الجهاز.",
            "مركز الطوارئ" => "أدوات حالة الاتصال. لا يوجد حالياً محرك تداول حي لإيقاف أوامر صادرة منه.",
            "الأخبار الاقتصادية" => "يتطلب موجز أخبار اقتصادي خارجي قبل عرض أخبار مباشرة.",
            "المساعد الصوتي العربي" => "واجهة ملاحظات نصية حالياً؛ التعرف الصوتي غير مربوط.",
            _ => "صفحة القسم جاهزة، والبيانات الخارجية ستظهر عند ربط مصدرها الفعلي."
        };

        private string GetSectionStatus(string section)
        {
            if (section == "مراقبة النظام والاتصال" || section == "الرئيسية" ||
                section == "مركز التحكم الرئيسي" || section == "الصفقات المفتوحة")
                return mt5Connected ? "متصل بـ MT5 - آخر قراءة من ملف الجسر." :
                    "غير متصل بـ MT5 - افتح المنصة وشغّل ملف ALZOOL_BRIDGE على XAUUSD.";
            return "الواجهة تعمل. مصدر البيانات أو محرك التنفيذ المطلوب لهذا القسم غير مربوط حالياً.";
        }

        private string GetSectionDetails(string section) => section switch
        {
            "عقل الذكاء الاصطناعي" => "الوحدات المطلوبة: تحليل السوق، إدارة الصفقات، وحماية رأس المال. الأولوية للحماية. هذه النسخة لا تدّعي تشغيل نموذج ذكاء اصطناعي أو اتخاذ قرارات تداول فعلية.",
            "تحليل السوق" => "يلزم توفير شموع XAUUSD ومؤشرات السوق. الجسر الحالي يقرأ السعر والرصيد والصفقات فقط، ولا يرسل بيانات الشموع.",
            "قرارات الذكاء الاصطناعي" => "لن يتم اختلاق BUY أو SELL أو WAIT. لا توجد إشارة فعلية حتى يتم ربط بيانات الشموع ومحرك التحليل.",
            "الصفقات المفتوحة" => "الجسر الحالي يرسل عدد الصفقات فقط، ولا يرسل تفاصيل كل صفقة أو يسمح بتعديلها أو إغلاقها.",
            "إدارة المخاطر" => "يمكنك ضبط الحدود من صفحة الإعدادات الثابتة. الحفظ محلي ولا يغيّر إعدادات حساب Deriv تلقائياً.",
            "مركز الطوارئ" => "لا تستخدم هذه النسخة للتداول الحقيقي. لا يمكنها إغلاق صفقات MT5 أو ضمان حماية الرصيد.",
            "الأخبار الاقتصادية" => "لا توجد خدمة أخبار متصلة في هذه النسخة؛ لذلك لن تظهر عناوين أو مواعيد غير موثقة.",
            "ماذا لو؟ محاكاة السوق" => "المحاكاة الحسابية غير مفعّلة بعد، ولن يتم عرض نتائج افتراضية على أنها نتائج اختبار.",
            "خريطة السيولة" => "تحتاج بيانات عمق السوق أو مزود سيولة. بيانات Bid وAsk وحدها لا تكفي لبناء خريطة سيولة موثوقة.",
            "توقع حركة الذهب" => "لا يوجد نموذج توقع متصل حالياً. أي توقع سعري دون بيانات واختبار سيكون مضللاً.",
            "منافسة الاستراتيجيات" => "لا توجد نتائج Backtest موثقة للمقارنة حالياً.",
            _ => "يمكنك كتابة ملاحظاتك وحفظها من هذه الصفحة. حفظ الملاحظات لا ينفذ تداولاً ولا يغيّر حساب MT5."
        };

        private bool ReadNumber(
            string[] data, int index, out double value)
        {
            value = 0;

            return data.Length > index &&
                double.TryParse(
                    data[index].Trim().Trim('"'),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out value);
        }


        private List<Candle> ReadBridgeCandles()
        {
            var candles = new List<Candle>();
            if (!File.Exists(candlesPath)) return candles;

            foreach (string line in File.ReadAllLines(candlesPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] fields = line.Split(';');
                if (fields.Length < 6) continue;

                string timeText = fields[0].Trim().Trim('"');
                string[] formats = { "yyyy.MM.dd HH:mm", "yyyy.MM.dd HH:mm:ss" };
                if (!DateTime.TryParseExact(timeText, formats,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal,
                    out DateTime time)) continue;

                if (!ReadNumber(fields, 1, out double open) ||
                    !ReadNumber(fields, 2, out double high) ||
                    !ReadNumber(fields, 3, out double low) ||
                    !ReadNumber(fields, 4, out double close) ||
                    !ReadNumber(fields, 5, out double volume)) continue;

                candles.Add(new Candle { Time = time, Open = open, High = high,
                    Low = low, Close = close, Volume = volume });
            }
            return candles.OrderBy(x => x.Time).ToList();
        }

        private static bool IsGoldSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return false;

            string normalized = symbol.Trim().ToUpperInvariant();

            // Brokers commonly append suffixes, for example XAUUSD.a or XAUUSDm.
            return normalized.StartsWith("XAUUSD", StringComparison.Ordinal) ||
                   (normalized.Contains("XAU", StringComparison.Ordinal) &&
                    normalized.Contains("USD", StringComparison.Ordinal));
        }

        private async Task RefreshBridgeAsync(bool showDetails)
        {
            bool connected = false;

            try
            {
                if (File.Exists(bridgePath))
                {
                    FileInfo info = new(bridgePath);

                    if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalSeconds <= 10)
                    {
                        // MT5 may rewrite the CSV while the app reads it.
                        // Allow shared read/write access to avoid false disconnects.
                        string content;
                        using (var stream = new FileStream(
                            bridgePath, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete))
                        using (var reader = new StreamReader(stream))
                        {
                            content = await reader.ReadToEndAsync();
                        }

                        string[] lines = content.Split(
                            new[] { '\r', '\n' },
                            StringSplitOptions.RemoveEmptyEntries);

                        if (lines.Length > 0)
                        {
                            string[] fields = lines[0].Split(';');

                            if (fields.Length >= 9 &&
                                ReadNumber(fields, 2, out double balance) &&
                                ReadNumber(fields, 3, out double equity) &&
                                ReadNumber(fields, 5, out double bid) &&
                                ReadNumber(fields, 6, out double ask) &&
                                int.TryParse(fields[7].Trim(), out int positions))
                            {
                                string login = fields[0].Trim();
                                string server = fields[1].Trim();
                                string symbol = fields[4].Trim();

                                connected =
                                    !string.IsNullOrWhiteSpace(login) &&
                                    !string.IsNullOrWhiteSpace(server) &&
                                    IsGoldSymbol(symbol);

                                if (connected)
                                {
                                    try { latestAnalysis = StrategyEngine.Analyze(ReadBridgeCandles()); }
                                    catch { latestAnalysis = null; }
                                }

                                if (connected && showDetails)
                                {
                                    SectionInfo.Text =
                                        $"الحساب: {login}\n" +
                                        $"الوسيط: {server}\n" +
                                        $"الرصيد: {balance:F2}\n" +
                                        $"حقوق الملكية: {equity:F2}\n" +
                                        $"سعر الشراء: {ask:F2}\n" +
                                        $"سعر البيع: {bid:F2}\n" +
                                        $"عدد الصفقات: {positions}\n" +
                                        (latestAnalysis == null
                                            ? "تحليل السوق: بانتظار 60 شمعة من MT5."
                                            : $"تحليل الذهب M1: {latestAnalysis.Description}\n" +
                                              $"RSI: {latestAnalysis.Rsi:F1} | ADX: {latestAnalysis.Adx:F1}\n" +
                                              $"قرار المحرك: {latestAnalysis.BestStrategy.Signal} - {latestAnalysis.BestStrategy.Name}\n" +
                                              $"السبب: {latestAnalysis.BestStrategy.Reason}");
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                connected = false;
            }

            mt5Connected = connected;

            ConnectionStatus.Text = connected
                ? "متصل - MT5"
                : "غير متصل";

            ConnectionStatus.Foreground = connected
                ? Brushes.LightGreen : Brushes.OrangeRed;

            // Execution remains disabled until demo order routing and risk checks pass.
            StartButton.IsEnabled = false;

            if (!connected && tradingControl.IsRunning)
                await tradingControl.StopAsync(() => Task.CompletedTask);

            if (showDetails && !connected)
            {
                MessageBox.Show(
                    "لم تصل بيانات حديثة من MT5.",
                    "الاتصال");
            }
        }

        private async void ConnectButton_Click(
            object sender, RoutedEventArgs e)
        {
            await RefreshBridgeAsync(true);
        }

        private void StartButton_Click(
            object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "تحليل السوق متصل ببيانات MT5. تنفيذ الأوامر ما زال معطلاً حتى اختبار جسر التداول والحماية على حساب تجريبي.",
                "تنبيه",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void StopButton_Click(
            object sender, RoutedEventArgs e)
        {
            await tradingControl.StopAsync(
                () => Task.CompletedTask);
        }
    }
}