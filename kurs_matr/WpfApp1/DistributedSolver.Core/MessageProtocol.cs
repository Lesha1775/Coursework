using System;
using System.IO;
using System.Text;

namespace DistributedSolver.Core
{
    public enum MessageType : byte
    {
        Initialize = 1,
        PivotRow = 2,
        GatherRows = 3,
        Shutdown = 4,
        Acknowledged = 100,
        RowData = 101,
        Error = 200
    }

    public static class MessageProtocol
    {
        public static void SendInitialize(BinaryWriter w, int size, int id, int[] rows)
        {
            w.Write((byte)MessageType.Initialize);
            w.Write(size);
            w.Write(id);
            w.Write(rows.Length);
            foreach (var r in rows) w.Write(r);
            w.Flush();
        }

        public static void SendPivotRow(BinaryWriter w, int step, double[] row)
        {
            w.Write((byte)MessageType.PivotRow);
            w.Write(step);
            w.Write(row.Length);
            foreach (var v in row) w.Write(v);
            w.Flush();
        }

        public static void SendGatherRows(BinaryWriter w)
        {
            w.Write((byte)MessageType.GatherRows);
            w.Flush();
        }

        public static void SendShutdown(BinaryWriter w)
        {
            w.Write((byte)MessageType.Shutdown);
            w.Flush();
        }

        public static void SendAcknowledged(BinaryWriter w, int step)
        {
            w.Write((byte)MessageType.Acknowledged);
            w.Write(step);
            w.Flush();
        }

        public static void SendRowData(BinaryWriter w, int rowIdx, double[] row)
        {
            w.Write((byte)MessageType.RowData);
            w.Write(rowIdx);
            w.Write(row.Length);
            foreach (var v in row) w.Write(v);
            w.Flush();
        }

        public static void SendError(BinaryWriter w, string message)
        {
            w.Write((byte)MessageType.Error);
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            w.Write(bytes.Length);
            w.Write(bytes);
            w.Flush();
        }
    }
}