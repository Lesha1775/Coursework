using System;

namespace DistributedSolver.Core
{
    public class Matrix
    {
        public double[][] Data { get; private set; }
        public int Size { get; private set; }

        public Matrix(int size)
        {
            Size = size;
            Data = new double[size][];
            for (int i = 0; i < size; i++)
                Data[i] = new double[size];
        }

        public Matrix(double[][] data)
        {
            Size = data.Length;
            Data = data;
        }

        public Matrix Clone()
        {
            var clone = new Matrix(Size);
            for (int i = 0; i < Size; i++)
                Array.Copy(Data[i], clone.Data[i], Size);
            return clone;
        }

        public (Matrix A, double[] b) GenerateSystem(int size, Random? rng = null)
        {
            rng = rng ?? new Random(42);
            var A = new Matrix(size);
            var b = new double[size];

            for (int i = 0; i < size; i++)
            {
                double rowSum = 0;
                for (int j = 0; j < size; j++)
                {
                    A.Data[i][j] = rng.NextDouble() * 10;
                    rowSum += Math.Abs(A.Data[i][j]);
                }
                A.Data[i][i] = rowSum + 1.0; // диагональное преобладание
                b[i] = rng.NextDouble() * 100;
            }
            return (A, b);
        }
    }
}