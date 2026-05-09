using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DistributedSolver.Core;
using WorkerNode; // для ClientHandler
using Xunit;

namespace DistributedSolver.Tests
{
    // --------------------------------------------------------------------------------
    // Тесты для Solver (последовательное LU и решение СЛАУ)
    // --------------------------------------------------------------------------------
    public class SolverTests
    {
        [Fact]
        public void SequentialLU_SmallMatrix_DecomposesCorrectly()
        {
            // Матрица 3x3 с известным точным решением
            var A = new Matrix(new double[][] {
                new double[] { 2, -1,  0 },
                new double[] {-1,  2, -1 },
                new double[] { 0, -1,  2 }
            });
            double[] b = { 1, 0, 1 };
            var expectedX = new[] { 1.0, 1.0, 1.0 };
            int[] pivot = new int[3];

            Solver.SequentialLU(A, pivot);
            double[] x = Solver.Solve(A, pivot, b);

            Assert.Equal(expectedX, x, new DoubleArrayComparer(6));
        }

        [Fact]
        public void Solve_RandomLargeSystem_MatchesDotProduct()
        {
            // Генерация случайной матрицы с диагональным преобладанием
            var ms = new Matrix(50);
            var (A, b) = ms.GenerateSystem(50);
            var originalA = A.Clone();
            var originalB = (double[])b.Clone();
            int[] pivot = new int[50];

            Solver.SequentialLU(A, pivot);
            double[] x = Solver.Solve(A, pivot, b);

            // Проверяем невязку: ||Ax - b||_∞ < 1e-9
            for (int i = 0; i < 50; i++)
            {
                double sum = 0;
                for (int j = 0; j < 50; j++)
                    sum += originalA.Data[i][j] * x[j];
                Assert.InRange(Math.Abs(sum - originalB[i]), 0, 1e-9);
            }
        }

        [Fact]
        public void SequentialLU_SingularMatrix_ThrowsException()
        {
            var A = new Matrix(new double[][] {
                new double[] { 1, 2 },
                new double[] { 2, 4 }
            });
            int[] pivot = new int[2];
            Assert.Throws<InvalidOperationException>(() => Solver.SequentialLU(A, pivot));
        }

        [Fact]
        public void Solve_WithCancellation_ThrowsOperationCanceledException()
        {
            var ms = new Matrix(20);
            var (A, b) = ms.GenerateSystem(20);
            int[] pivot = new int[20];
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => Solver.SequentialLU(A, pivot, cts.Token));
        }
    }

    // --------------------------------------------------------------------------------
    // Тесты для Matrix (генерация, клонирование)
    // --------------------------------------------------------------------------------
    public class MatrixTests
    {
        [Fact]
        public void GenerateSystem_HasDiagonalDominance()
        {
            var ms = new Matrix(10);
            var (A, b) = ms.GenerateSystem(10);

            for (int i = 0; i < 10; i++)
            {
                double rowSum = A.Data[i].Sum(Math.Abs) - Math.Abs(A.Data[i][i]);
                Assert.True(A.Data[i][i] > rowSum);
            }
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var original = new Matrix(3);
            var (genA, _) = original.GenerateSystem(3);
            var clone = genA.Clone();

            Assert.NotSame(genA.Data, clone.Data);
            for (int i = 0; i < 3; i++)
            {
                Assert.NotSame(genA.Data[i], clone.Data[i]);
                Assert.True(genA.Data[i].SequenceEqual(clone.Data[i]));
            }

            clone.Data[0][0] = 999;
            Assert.NotEqual(genA.Data[0][0], clone.Data[0][0]);
        }
    }

    // --------------------------------------------------------------------------------
    // Тесты для MessageProtocol (сериализация/десериализация)
    // --------------------------------------------------------------------------------
    public class MessageProtocolTests
    {
        [Fact]
        public void Roundtrip_Initialize_BytesMatch()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            using var reader = new BinaryReader(new MemoryStream());

            var rows = new[] { 2, 5, 7 };
            MessageProtocol.SendInitialize(writer, 10, 1, rows);
            ms.Seek(0, SeekOrigin.Begin);

            // Передаём в новый reader
            var ms2 = new MemoryStream(ms.ToArray());
            var reader2 = new BinaryReader(ms2);

            Assert.Equal(MessageType.Initialize, (MessageType)reader2.ReadByte());
            Assert.Equal(10, reader2.ReadInt32());
            Assert.Equal(1, reader2.ReadInt32());
            int count = reader2.ReadInt32();
            Assert.Equal(3, count);
            for (int i = 0; i < count; i++)
                Assert.Equal(rows[i], reader2.ReadInt32());
        }

        [Fact]
        public void SendError_AndReadError_MatchesMessage()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            MessageProtocol.SendError(writer, "Test error");
            ms.Seek(0, SeekOrigin.Begin);

            var reader = new BinaryReader(ms);
            Assert.Equal(MessageType.Error, (MessageType)reader.ReadByte());
            int len = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(len);
            string msg = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Equal("Test error", msg);
        }
    }


    // --------------------------------------------------------------------------------
    // Вспомогательный компаратор для массивов double с погрешностью
    // --------------------------------------------------------------------------------
    public class DoubleArrayComparer : IEqualityComparer<double[]>
    {
        private readonly int _precisionDigits;
        public DoubleArrayComparer(int precisionDigits = 6) => _precisionDigits = precisionDigits;

        public bool Equals(double[] x, double[] y)
        {
            if (x.Length != y.Length) return false;
            double tolerance = Math.Pow(10, -_precisionDigits);
            for (int i = 0; i < x.Length; i++)
                if (Math.Abs(x[i] - y[i]) > tolerance) return false;
            return true;
        }

        public int GetHashCode(double[] obj) => obj.GetHashCode();
    }
}