using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedSolver.Core
{
    public class WorkerClient : IDisposable
    {
        private readonly TcpClient _client;
        private readonly BinaryWriter _w;
        private readonly BinaryReader _r;
        private readonly ILogger _logger;
        private Dictionary<int, double[]> _localRows;
        private const int ReadTimeoutMs = 30000; // 30 seconds timeout

        public WorkerClient(TcpClient client, ILogger logger)
        {
            _client = client;
            _logger = logger;
            _client.ReceiveTimeout = ReadTimeoutMs;
            _client.SendTimeout = ReadTimeoutMs;
            var s = client.GetStream();
            _r = new BinaryReader(s);
            _w = new BinaryWriter(s);
            _localRows = new Dictionary<int, double[]>();
        }

        public void Initialize(int size, int id, int[] rows, Matrix fullMatrix)
        {
            MessageProtocol.SendInitialize(_w, size, id, rows);
            ReadAcknowledged();
            
            foreach (var rowIdx in rows)
            {
                double[] row = new double[size];
                Array.Copy(fullMatrix.Data[rowIdx], row, size);
                MessageProtocol.SendRowData(_w, rowIdx, row);
                ReadAcknowledged();
                _localRows[rowIdx] = row;
            }
        }

        public void ProcessPivotRow(int step, double[] pivotRow)
        {
            MessageProtocol.SendPivotRow(_w, step, pivotRow);
            ReadAcknowledged();
        }

        public List<(int rowIdx, double[] row)> GatherRows()
        {
            MessageProtocol.SendGatherRows(_w);
            var result = new List<(int, double[])>();
            while (true)
            {
                var msg = (MessageType)_r.ReadByte();
                if (msg == MessageType.RowData)
                {
                    int idx = _r.ReadInt32();
                    int len = _r.ReadInt32();
                    double[] row = new double[len];
                    for (int i = 0; i < len; i++) row[i] = _r.ReadDouble();
                    result.Add((idx, row));
                }
                else if (msg == MessageType.Acknowledged) break;
                else if (msg == MessageType.Error) throw ReadError();
                else throw new Exception($"Неожиданное сообщение {msg}");
            }
            return result;
        }

        public void Shutdown()
        {
            try { MessageProtocol.SendShutdown(_w); } catch { }
        }

        public void Dispose()
        {
            Shutdown();
            _client?.Close();
            _w?.Dispose();
            _r?.Dispose();
        }

        private void ReadAcknowledged()
        {
            var msg = (MessageType)_r.ReadByte();
            if (msg == MessageType.Error) throw ReadError();
            if (msg != MessageType.Acknowledged)
                throw new Exception($"Ожидался Acknowledged, получен {msg}");
            _ = _r.ReadInt32(); // step
        }

        private Exception ReadError()
        {
            int len = _r.ReadInt32();
            byte[] err = _r.ReadBytes(len);
            return new Exception(System.Text.Encoding.UTF8.GetString(err));
        }
    }
}