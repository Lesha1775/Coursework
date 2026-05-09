using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedSolver.Core
{
    public class DistributedLUManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly List<WorkerClient> _workers = new List<WorkerClient>();
        private int _matrixSize;
        private Matrix? _fullMatrix;

        public DistributedLUManager(ILogger logger) => _logger = logger ?? new NullLogger();

        public void ConnectWorker(string host, int port)
        {
            _logger.Log($"Подключение к {host}:{port}");
            var client = new TcpClient(host, port);
            var worker = new WorkerClient(client, _logger);
            _workers.Add(worker);
        }

        public void DistributeMatrix(Matrix A)
        {
            _matrixSize = A.Size;
            _fullMatrix = A.Clone();

            var assignments = new List<int>[_workers.Count];
            for (int i = 0; i < _workers.Count; i++) assignments[i] = new List<int>();

            for (int row = 0; row < _matrixSize; row++)
                assignments[row % _workers.Count].Add(row);

            for (int i = 0; i < _workers.Count; i++)
            {
                var rows = assignments[i].ToArray();
                _workers[i].Initialize(_matrixSize, i, rows, _fullMatrix);
            }
        }

        public (Matrix LU, int[] pivot) ComputeLU(CancellationToken token = default)
        {
            int n = _matrixSize;
            int[] pivot = new int[n];
            var LU = _fullMatrix.Clone();
            var sw = Stopwatch.StartNew();

            for (int step = 0; step < n; step++)
            {
                token.ThrowIfCancellationRequested();

                // выбор ведущего (pivot) мастером локально
                int pivotRow = step;
                double maxVal = Math.Abs(LU.Data[step][step]);
                for (int r = step + 1; r < n; r++)
                {
                    if (Math.Abs(LU.Data[r][step]) > maxVal)
                    {
                        maxVal = Math.Abs(LU.Data[r][step]);
                        pivotRow = r;
                    }
                }
                pivot[step] = pivotRow;
                if (pivotRow != step)
                {
                    var tmp = LU.Data[step];
                    LU.Data[step] = LU.Data[pivotRow];
                    LU.Data[pivotRow] = tmp;
                }

                // опорная строка для рассылки
                double[] pivotRowData = new double[n - step];
                Array.Copy(LU.Data[step], step, pivotRowData, 0, n - step);

                var tasks = new List<Task>();
                foreach (var w in _workers)
                    tasks.Add(Task.Run(() => w.ProcessPivotRow(step, pivotRowData), token));
                Task.WaitAll(tasks.ToArray());
            }

            // сбор строк с воркеров
            foreach (var w in _workers)
            {
                var rows = w.GatherRows();
                foreach (var (idx, data) in rows)
                    Array.Copy(data, LU.Data[idx], n);
            }

            _logger.Log($"LU-разложение завершено за {sw.ElapsedMilliseconds} мс.");
            return (LU, pivot);
        }

        public double[] Solve(Matrix LU, int[] pivot, double[] b, CancellationToken token = default)
            => Solver.Solve(LU, pivot, b, token);

        public void ShutdownWorkers()
        {
            foreach (var w in _workers) w.Shutdown();
        }

        public void Dispose()
        {
            ShutdownWorkers();
            foreach (var w in _workers) w.Dispose();
        }
    }
}