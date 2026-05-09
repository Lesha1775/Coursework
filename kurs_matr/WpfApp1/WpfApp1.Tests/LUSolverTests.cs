using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using WpfApp1;

namespace WpfApp1.Tests
{
    public class LUSolverTests
    {
        private readonly ITestOutputHelper _output;

        public LUSolverTests(ITestOutputHelper output)
        {
            _output = output; // Позволяет писать логи прямо в окно тестов Visual Studio
        }

        // --- МОДУЛЬНЫЕ ТЕСТЫ ---

        [Fact]
        public void SequentialLU_CorrectlySolves_System3x3()
        {
            double[,] A = { { 2, -1, 0 }, { -1, 2, -1 }, { 0, -1, 2 } };
            double[] b = { 1, 0, 1 };
            int n = 3;

            var (L, U) = LUSolver.SequentialLU(A, n);
            var x = LUSolver.Solve(L, U, b, n);

            // Ожидаемый результат: x = {1, 1, 1}
            Assert.Equal(1.0, x[0], precision: 5);
            Assert.Equal(1.0, x[1], precision: 5);
            Assert.Equal(1.0, x[2], precision: 5);
        }

        [Fact]
        public void ParallelLU_CorrectlySolves_System3x3()
        {
            double[,] A = { { 2, -1, 0 }, { -1, 2, -1 }, { 0, -1, 2 } };
            double[] b = { 1, 0, 1 };
            int n = 3;

            var (L, U) = LUSolver.ParallelLU(A, n);
            var x = LUSolver.Solve(L, U, b, n);

            Assert.Equal(1.0, x[0], precision: 5);
            Assert.Equal(1.0, x[1], precision: 5);
            Assert.Equal(1.0, x[2], precision: 5);
        }

        [Fact]
        public void BothApproaches_YieldSameResults_OnRandomSystem()
        {
            int n = 100;
            var (A, b) = LUSolver.GenerateSystem(n);

            var (L1, U1) = LUSolver.SequentialLU(A, n);
            var xSeq = LUSolver.Solve(L1, U1, b, n);

            var (L2, U2) = LUSolver.ParallelLU(A, n);
            var xPar = LUSolver.Solve(L2, U2, b, n);

            for (int i = 0; i < n; i++)
            {
                Assert.Equal(xSeq[i], xPar[i], precision: 6);
            }
        }

        // --- НАГРУЗОЧНЫЕ ТЕСТЫ ---

        [Theory]
        [InlineData(250)]
        [InlineData(500)]
        [InlineData(1000)]
        public void LoadTest_CompareExecutionTimes(int n)
        {
            var (A, b) = LUSolver.GenerateSystem(n);

            // JIT Прогрев
            LUSolver.SequentialLU(A, 10);
            LUSolver.ParallelLU(A, 10);

            var swSeq = Stopwatch.StartNew();
            var (L1, U1) = LUSolver.SequentialLU(A, n);
            LUSolver.Solve(L1, U1, b, n);
            swSeq.Stop();

            var swPar = Stopwatch.StartNew();
            var (L2, U2) = LUSolver.ParallelLU(A, n);
            LUSolver.Solve(L2, U2, b, n);
            swPar.Stop();

            _output.WriteLine($"Размерность N: {n}");
            _output.WriteLine($"Последовательное: {swSeq.ElapsedMilliseconds} ms");
            _output.WriteLine($"Параллельное (PLINQ): {swPar.ElapsedMilliseconds} ms");
            
            double speedup = (double)swSeq.ElapsedMilliseconds / Math.Max(1, swPar.ElapsedMilliseconds);
            _output.WriteLine($"Ускорение: {speedup:F2}x");

            // При больших N параллельное выполнение должно быть быстрее (или хотя бы работать стабильно)
            Assert.True(swPar.ElapsedMilliseconds <= swSeq.ElapsedMilliseconds * 1.5, "Параллельная версия сильно деградировала по времени");
        }
    }
}       