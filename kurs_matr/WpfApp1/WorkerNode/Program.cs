using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DistributedSolver.Core;

namespace WorkerNode
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int port = args.Length > 0 ? int.Parse(args[0]) : 8888;
            var logger = new ConsoleLogger();
            logger.Log($"Worker запущен на порту {port}");

            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            logger.Log("Ожидание подключений...");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                logger.Log("Новое подключение.");
                _ = Task.Run(() => new ClientHandler(client, logger).Process());
            }
        }
    }
}