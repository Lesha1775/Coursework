using System;
using System.Threading;

namespace DistributedSolver.Core
{
    public static class Solver
    {
        public static void SequentialLU(Matrix A, int[] pivot, CancellationToken token = default)
        {
            int n = A.Size;
            for (int i = 0; i < n; i++)
            {
                token.ThrowIfCancellationRequested();

                // частичный выбор ведущего элемента
                int pivotRow = i;
                double maxVal = Math.Abs(A.Data[i][i]);
                for (int r = i + 1; r < n; r++)
                {
                    if (Math.Abs(A.Data[r][i]) > maxVal)
                    {
                        maxVal = Math.Abs(A.Data[r][i]);
                        pivotRow = r;
                    }
                }
                pivot[i] = pivotRow;

                if (pivotRow != i)
                {
                    var temp = A.Data[i];
                    A.Data[i] = A.Data[pivotRow];
                    A.Data[pivotRow] = temp;
                }

                double diag = A.Data[i][i];
                if (Math.Abs(diag) < 1e-12)
                    throw new InvalidOperationException("Матрица близка к вырожденной.");

                for (int j = i + 1; j < n; j++)
                {
                    A.Data[j][i] /= diag;
                    double factor = A.Data[j][i];
                    for (int k = i + 1; k < n; k++)
                        A.Data[j][k] -= factor * A.Data[i][k];
                }
            }
        }

        public static double[] Solve(Matrix LU, int[] pivot, double[] b, CancellationToken token = default)
        {
            int n = LU.Size;
            double[] pb = new double[n];
            for (int i = 0; i < n; i++)
                pb[i] = b[pivot[i]];

            double[] y = new double[n];
            for (int i = 0; i < n; i++)
            {
                token.ThrowIfCancellationRequested();
                double sum = 0;
                for (int j = 0; j < i; j++)
                    sum += LU.Data[i][j] * y[j];
                y[i] = pb[i] - sum;
            }

            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                token.ThrowIfCancellationRequested();
                double sum = 0;
                for (int j = i + 1; j < n; j++)
                    sum += LU.Data[i][j] * x[j];
                x[i] = (y[i] - sum) / LU.Data[i][i];
            }
            return x;
        }
    }
}