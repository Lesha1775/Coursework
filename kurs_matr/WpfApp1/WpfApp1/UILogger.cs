using System;
using DistributedSolver.Core;

namespace WpfApp1
{
    public class UILogger : ILogger
    {
        private readonly Action<string> _logAction;
        public UILogger(Action<string> logAction) => _logAction = logAction;
        public void Log(string message) => _logAction(message);
        public void LogError(string message) => _logAction($"[ERROR] {message}");
    }
}