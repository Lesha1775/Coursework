using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DistributedSolver.Core;   // для ILogger
using WorkerNode;             // для ClientHandler

namespace WpfApp1
{
    public class LocalWorker
    {
        private readonly int _port;
        private TcpListener? _listener;
        private Task? _listenTask;

        public LocalWorker(int port) => _port = port;

        public void Start(ILogger logger)
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            logger.Log($"Локальный воркер запущен на {IPAddress.Loopback}:{_port}");
            _listenTask = Task.Run(async () =>
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    logger.Log("Воркер принял подключение.");
                    _ = Task.Run(() => new ClientHandler(client, logger).Process());
                }
            });
        }

        public void Stop()
        {
            _listener?.Stop();
        }
    }
}