using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DistributedSolver.Core;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cts;
        private DistributedLUManager? _manager;
        private LocalWorker? _localWorker;
        private double[]? _lastResult;
        private int _lastSize;
        private long _lastTimeMs;
        private string _lastMode = "";

        public MainWindow() => InitializeComponent();

        private void Log(string msg) => Dispatcher.BeginInvoke(new Action(() =>
        {
            LogBox.AppendText($"[{DateTime.Now:T}] {msg}\n");
            LogBox.ScrollToEnd();
        }));

        private async void BtnSeq_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(MatrixSizeTextBox.Text, out int n) || n <= 0) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SetButtons(false);
            Log($"=== Последовательное LU (N={n}) ===");
            var sw = Stopwatch.StartNew();
            try
            {
                var m = new Matrix(n);
                var (A, b) = await Task.Run(() => m.GenerateSystem(n), token);
                int[] piv = new int[n];
                await Task.Run(() => Solver.SequentialLU(A, piv, token), token);
                double[] x = Solver.Solve(A, piv, b, token);
                sw.Stop();
                long elapsed = sw.ElapsedMilliseconds;
                Log($"Решено за {elapsed} мс.");
                ShowX(x);

                _lastResult = x;
                _lastSize = n;
                _lastTimeMs = elapsed;
                _lastMode = "Последовательное LU";
                Dispatcher.BeginInvoke(new Action(() => FileSavedLabel.Text = ""));
            }
            catch (OperationCanceledException) { Log("Отменено."); }
            catch (Exception ex) { Log($"Ошибка: {ex.Message}"); }
            finally { Cleanup(); }
        }

        private async void BtnDist_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(MatrixSizeTextBox.Text, out int n) || n <= 0) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SetButtons(false);
            Log($"=== Распределённое LU (N={n}) ===");
            var sw = Stopwatch.StartNew();
            try
            {
                var addresses = WorkersBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(a => a.Trim()).ToArray();
                if (addresses.Length == 0) { Log("Нет адресов воркеров."); return; }

                var uiLogger = new UILogger(Log);
                _manager = new DistributedLUManager(uiLogger);

                foreach (var addr in addresses)
                {
                    var parts = addr.Split(':');
                    await Task.Run(() => _manager.ConnectWorker(parts[0], int.Parse(parts[1])), token);
                }

                var matrix = new Matrix(n);
                var (A, b) = await Task.Run(() => matrix.GenerateSystem(n), token);
                _manager.DistributeMatrix(A);

                var (LU, piv) = await Task.Run(() => _manager.ComputeLU(token), token);
                double[] x = _manager.Solve(LU, piv, b, token);
                sw.Stop();
                long elapsed = sw.ElapsedMilliseconds;
                Log($"Решено за {elapsed} мс.");
                ShowX(x);

                _lastResult = x;
                _lastSize = n;
                _lastTimeMs = elapsed;
                _lastMode = "Распределённое LU";
                Dispatcher.BeginInvoke(new Action(() => FileSavedLabel.Text = ""));
            }
            catch (OperationCanceledException) { Log("Отменено."); }
            catch (Exception ex) { Log($"Ошибка: {ex.Message}"); }
            finally { Cleanup(); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

        private void BtnClear_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

        private void StartLocal_Click(object sender, RoutedEventArgs e)
        {
            if (_localWorker != null) return;
            int port = 8888;
            _localWorker = new LocalWorker(port);
            _localWorker.Start(new UILogger(Log));
            Log("Локальный воркер запущен.");
        }

        private void SaveResult_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null)
            {
                Dispatcher.BeginInvoke(new Action(() => FileSavedLabel.Text = "Нет данных для сохранения."));
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    string format = "";
                    Dispatcher.Invoke(() => format = ((System.Windows.Controls.ComboBoxItem)FileFormatCombo.SelectedItem).Content.ToString() ?? "TXT");

                    string content = "";
                    string extension = "";
                    switch (format)
                    {
                        case "TXT":
                            extension = "txt";
                            content = GenerateTxtContent();
                            break;
                        case "CSV":
                            extension = "csv";
                            content = GenerateCsvContent();
                            break;
                        case "Word (XML)":
                            extension = "xml";
                            content = GenerateWordXmlContent();
                            break;
                        default:
                            extension = "txt";
                            content = GenerateTxtContent();
                            break;
                    }

                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = $"LU_Result_N{_lastSize}_{timestamp}.{extension}";
                    string filePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        fileName);

                    File.WriteAllText(filePath, content, Encoding.UTF8);

                    Log($"Файл сохранён: {filePath}");
                    Dispatcher.BeginInvoke(new Action(() => FileSavedLabel.Text = $"Сохранено: {fileName}"));
                }
                catch (Exception ex)
                {
                    Log($"Ошибка сохранения файла: {ex.Message}");
                    Dispatcher.BeginInvoke(new Action(() => FileSavedLabel.Text = "Ошибка сохранения."));
                }
            });
        }

        private string GenerateTxtContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("  РЕЗУЛЬТАТЫ РЕШЕНИЯ СЛАУ МЕТОДОМ LU-РАЗЛОЖЕНИЯ");
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine($"Режим: {_lastMode}");
            sb.AppendLine($"Размерность матрицы: {_lastSize} × {_lastSize}");
            sb.AppendLine($"Время решения: {_lastTimeMs} мс");
            sb.AppendLine($"Дата и время: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("ВЕКТОР НЕИЗВЕСТНЫХ (X)");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("Индекс          Значение");
            sb.AppendLine("───────────────────────────────────────────");

            int displayCount = Math.Min(_lastResult!.Length, 100);
            for (int i = 0; i < displayCount; i++)
            {
                sb.AppendLine($"x[{i,6}]  {_lastResult[i],20:F12}");
            }

            if (_lastResult.Length > 100)
            {
                sb.AppendLine("...");
                sb.AppendLine($"x[{_lastResult.Length - 1,6}]  {_lastResult[_lastResult.Length - 1],20:F12}");
                sb.AppendLine($"(Показаны первые 100 элементов из {_lastResult.Length})");
            }

            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }

        private string GenerateCsvContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Режим,{_lastMode}");
            sb.AppendLine($"# Размерность,{_lastSize}");
            sb.AppendLine($"# Время решения (мс),{_lastTimeMs}");
            sb.AppendLine($"# Дата,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("Индекс,Значение");

            int displayCount = Math.Min(_lastResult!.Length, 5000);
            for (int i = 0; i < displayCount; i++)
            {
                sb.AppendLine($"{i},{_lastResult[i].ToString("F12", CultureInfo.InvariantCulture)}");
            }

            if (_lastResult.Length > 5000)
            {
                sb.AppendLine("...,...");
                sb.AppendLine($"{_lastResult.Length - 1},{_lastResult[_lastResult.Length - 1].ToString("F12", CultureInfo.InvariantCulture)}");
            }

            return sb.ToString();
        }

        private string GenerateWordXmlContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<?mso-application progid=\"Word.Document\"?>");
            sb.AppendLine("<w:wordDocument xmlns:w=\"http://schemas.microsoft.com/office/word/2003/wordml\">");
            sb.AppendLine("<w:body>");
            sb.AppendLine("<w:p><w:r><w:t>РЕЗУЛЬТАТЫ РЕШЕНИЯ СЛАУ МЕТОДОМ LU-РАЗЛОЖЕНИЯ</w:t></w:r></w:p>");
            sb.AppendLine("<w:p/>");
            sb.AppendLine($"<w:p><w:r><w:t>Режим: {_lastMode}</w:t></w:r></w:p>");
            sb.AppendLine($"<w:p><w:r><w:t>Размерность матрицы: {_lastSize} × {_lastSize}</w:t></w:r></w:p>");
            sb.AppendLine($"<w:p><w:r><w:t>Время решения: {_lastTimeMs} мс</w:t></w:r></w:p>");
            sb.AppendLine($"<w:p><w:r><w:t>Дата и время: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</w:t></w:r></w:p>");
            sb.AppendLine("<w:p/>");
            sb.AppendLine("<w:p><w:r><w:t>ВЕКТОР НЕИЗВЕСТНЫХ (X)</w:t></w:r></w:p>");
            sb.AppendLine("<w:p/>");
            sb.AppendLine("<w:tbl>");
            sb.AppendLine("<w:tr>");
            sb.AppendLine("<w:tc><w:p><w:r><w:t>Индекс</w:t></w:r></w:p></w:tc>");
            sb.AppendLine("<w:tc><w:p><w:r><w:t>Значение</w:t></w:r></w:p></w:tc>");
            sb.AppendLine("</w:tr>");

            int displayCount = Math.Min(_lastResult!.Length, 100);
            for (int i = 0; i < displayCount; i++)
            {
                sb.AppendLine("<w:tr>");
                sb.AppendLine($"<w:tc><w:p><w:r><w:t>x[{i}]</w:t></w:r></w:p></w:tc>");
                sb.AppendLine($"<w:tc><w:p><w:r><w:t>{_lastResult[i]:F12}</w:t></w:r></w:p></w:tc>");
                sb.AppendLine("</w:tr>");
            }

            if (_lastResult.Length > 100)
            {
                sb.AppendLine("<w:tr>");
                sb.AppendLine("<w:tc><w:p><w:r><w:t>...</w:t></w:r></w:p></w:tc>");
                sb.AppendLine("<w:tc><w:p><w:r><w:t>...</w:t></w:r></w:p></w:tc>");
                sb.AppendLine("</w:tr>");
                sb.AppendLine("<w:tr>");
                sb.AppendLine($"<w:tc><w:p><w:r><w:t>x[{_lastResult.Length - 1}]</w:t></w:r></w:p></w:tc>");
                sb.AppendLine($"<w:tc><w:p><w:r><w:t>{_lastResult[_lastResult.Length - 1]:F12}</w:t></w:r></w:p></w:tc>");
                sb.AppendLine("</w:tr>");
            }

            sb.AppendLine("</w:tbl>");
            sb.AppendLine("</w:body>");
            sb.AppendLine("</w:wordDocument>");

            return sb.ToString();
        }

        private void ShowX(double[] x)
        {
            int show = Math.Min(5, x.Length);
            string s = string.Join(", ", x.Take(show).Select(v => v.ToString("F4")));
            if (x.Length > show) s += $", ... x[{x.Length - 1}] = {x.Last():F4}";
            Log($"Вектор X: [{s}]");
        }

        private void SetButtons(bool enabled)
        {
            BtnSeq.IsEnabled = BtnDist.IsEnabled = MatrixSizeTextBox.IsEnabled = WorkersBox.IsEnabled = enabled;
            BtnCancel.IsEnabled = !enabled;
        }

        private void Cleanup()
        {
            _manager?.Dispose();
            _manager = null;
            _cts?.Dispose();
            _cts = null;
            SetButtons(true);
        }

        protected override void OnClosed(EventArgs e)
        {
            _localWorker?.Stop();
            Cleanup();
            base.OnClosed(e);
        }
    }
}