using System;

namespace DistributedSolver.Core
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine($"[INFO] {DateTime.Now:T} {message}");
        public void LogError(string message) => Console.WriteLine($"[ERROR] {DateTime.Now:T} {message}");
    }

    public class NullLogger : ILogger
    {
        public void Log(string message) { }
        public void LogError(string message) { }
    }
}