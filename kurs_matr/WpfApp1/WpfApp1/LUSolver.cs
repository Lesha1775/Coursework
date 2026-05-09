using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace WpfApp1
{
    public static class LUSolver
    {
        // Последовательное решение (In-Place)
        public static double[][] SequentialLU(double[][] A, int n, CancellationToken token = default)
        {
            for (int i = 0; i < n; i++)
            {
                token.ThrowIfCancellationRequested();
                
                for (int j = i; j < n; j++)
                {
                    double sumForU = 0;
                    for (int k = 0; k < i; k++) sumForU += A[i][k] * A[k][j];
                    A[i][j] -= sumForU;
                }
                
                for (int j = i + 1; j < n; j++)
                {
                    double sumForL = 0;
                    for (int k = 0; k < i; k++) sumForL += A[j][k] * A[k][i];
                    A[j][i] = (A[j][i] - sumForL) / A[i][i];
                }
            }
            return A;
        }

        // Параллельное (распределенное) решение (In-Place)
        public static double[][] ParallelLU(double[][] A, int n, CancellationToken token = default)
        {
            var options = new ParallelOptions { CancellationToken = token };
            
            for (int i = 0; i < n; i++)
            {
                token.ThrowIfCancellationRequested();

                Parallel.For(i, n, options, j =>
                {
                    double sumU = 0;
                    for (int k = 0; k < i; k++) sumU += A[i][k] * A[k][j];
                    A[i][j] -= sumU;
                });

                Parallel.For(i + 1, n, options, j =>
                {
                    double sumL = 0;
                    for (int k = 0; k < i; k++) sumL += A[j][k] * A[k][i];
                    A[j][i] = (A[j][i] - sumL) / A[i][i];
                });
            }

            return A;
        }

        // Сетевое распределенное решение через сокеты (Академическая реализация)
        public static void DistributedNetworkLU(double[][] A, int n, TcpClient worker)
        {
            using var stream = worker.GetStream();
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);

            // Отправляем чанками на Worker-ноду (Упрощенный вариант, только для примера работы по сети)
            // В реальном проекте Worker должен делать больше работы (целые блоки)
            for (int i = 0; i < n; i++)
            {
                writer.Write(1); // cmd: Compute
                writer.Write(n);
                writer.Write(i);
                
                // pivot row
                for (int col = 0; col < n; col++) writer.Write(A[i][col]);

                // кол-во строк для отправки
                int sendCount = n - i; 
                writer.Write(sendCount);

                for (int j = i; j < n; j++)
                {
                    writer.Write(j);
                    for (int col = 0; col < n; col++) writer.Write(A[j][col]);
                }

                // Получаем результат
                for (int count = 0; count < sendCount; count++)
                {
                    int j = reader.ReadInt32();
                    for (int col = 0; col < n; col++) A[j][col] = reader.ReadDouble();
                }
            }
            
            writer.Write(0); // Остановка воркера
        }

        // Решение системы Ly = b, потом Ux = y
        public static double[] Solve(double[][] LU, double[] b, int n, CancellationToken token = default)
        {
            double[] y = new double[n];
            for (int i = 0; i < n; i++)
            {
                token.ThrowIfCancellationRequested();
                double sum = 0;
                for (int j = 0; j < i; j++) sum += LU[i][j] * y[j];
                y[i] = b[i] - sum; // Элементы главной диагонали L равны 1
            }

            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                token.ThrowIfCancellationRequested();
                double sum = 0;
                for (int j = i + 1; j < n; j++) sum += LU[i][j] * x[j];
                x[i] = (y[i] - sum) / LU[i][i];
            }

            return x;
        }

        // Генерация данных
        public static (double[][] A, double[] b) GenerateSystem(int size, CancellationToken token = default)
        {
            var A = new double[size][];
            var b = new double[size];
            var rand = new Random(42); 

            for (int i = 0; i < size; i++)
            {
                token.ThrowIfCancellationRequested();
                A[i] = new double[size];
                double rowSum = 0;
                for (int j = 0; j < size; j++)
                {
                    A[i][j] = rand.NextDouble() * 10;
                    rowSum += Math.Abs(A[i][j]);
                }
                
                A[i][i] = rowSum + 1.0; 
                b[i] = rand.NextDouble() * 100;
            }

            return (A, b);
        }
    }
}