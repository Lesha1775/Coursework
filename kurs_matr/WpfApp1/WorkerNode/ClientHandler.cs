using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using DistributedSolver.Core;

namespace WorkerNode
{
    public class ClientHandler
    {
        private readonly TcpClient _client;
        private readonly ILogger _logger;
        private BinaryReader? _r;
        private BinaryWriter? _w;
        private int _size;
        private int[]? _rows;
        private Dictionary<int, double[]> _local;
        private const int ReadTimeoutMs = 30000;

        public ClientHandler(TcpClient client, ILogger logger)
        {
            _client = client;
            _logger = logger;
            _local = new Dictionary<int, double[]>();
            _rows = Array.Empty<int>();
            _client.ReceiveTimeout = ReadTimeoutMs;
            _client.SendTimeout = ReadTimeoutMs;
        }

        public void Process()
        {
            using var s = _client.GetStream();
            _r = new BinaryReader(s);
            _w = new BinaryWriter(s);

            try
            {
                while (true)
                {
                    var msg = (MessageType)_r.ReadByte();
                    switch (msg)
                    {
                        case MessageType.Initialize: HandleInit(); break;
                        case MessageType.RowData: HandleRow(); break;
                        case MessageType.PivotRow: HandlePivot(); break;
                        case MessageType.GatherRows: HandleGather(); return;
                        case MessageType.Shutdown: return;
                        default: throw new Exception($"Неизвестный код {msg}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка: {ex.Message}");
                try { MessageProtocol.SendError(_w, ex.Message); } catch { }
            }
            finally { _client.Close(); }
        }

        private void HandleInit()
        {
            _size = _r.ReadInt32();
            int id = _r.ReadInt32();
            int cnt = _r.ReadInt32();
            _rows = new int[cnt];
            for (int i = 0; i < cnt; i++) _rows[i] = _r.ReadInt32();
            _logger.Log($"Init: размер={_size}, id={id}, строк={cnt}");
            MessageProtocol.SendAcknowledged(_w, -1);
        }

        private void HandleRow()
        {
            int idx = _r.ReadInt32();
            int len = _r.ReadInt32();
            double[] row = new double[len];
            for (int i = 0; i < len; i++) row[i] = _r.ReadDouble();
            _local[idx] = row;

            // Send acknowledgement immediately for each row
            MessageProtocol.SendAcknowledged(_w, idx);
        }

        private void HandlePivot()
        {
            int step = _r.ReadInt32();
            int len = _r.ReadInt32();
            double[] pivotRow = new double[len];
            for (int i = 0; i < len; i++) pivotRow[i] = _r.ReadDouble();

            double diag = pivotRow[0];
            if (Math.Abs(diag) < 1e-12)
            {
                MessageProtocol.SendError(_w, "Деление на ноль");
                return;
            }

            foreach (var kv in _local)
            {
                int rowIdx = kv.Key;
                if (rowIdx <= step) continue;
                double factor = kv.Value[step] / diag;
                kv.Value[step] = factor;
                for (int k = step + 1; k < _size; k++)
                    kv.Value[k] -= factor * pivotRow[k - step];
            }
            MessageProtocol.SendAcknowledged(_w, step);
        }

        private void HandleGather()
        {
            _logger.Log("Сбор строк...");
            foreach (var (idx, row) in _local)
                MessageProtocol.SendRowData(_w, idx, row);
            MessageProtocol.SendAcknowledged(_w, -1);
        }
    }
}