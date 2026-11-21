using System;
using System.Collections.Generic;

namespace Motel
{
    // Pure scene-owned board state: clockwise bits N/E/S/W, row-major cells; no timer or persistence.
    public sealed class BypassCircuit
    {
        public const int Size = 4, Input = 4, Output = 7, Burned = 5;
        public const int N = 1, E = 2, S = 4, W = 8;
        private readonly int[] ports = { N|E, N|S, N|E, W|E, W|N, 0, W|E, W|N, 0, W|E, S|W, S|E, 0, 0, 0, 0 };
        private readonly bool[] powered = new bool[Size * Size];
        public bool IsComplete => powered[Output] && (ports[Output] & E) != 0;
        public BypassCircuit() { Trace(); }
        public int Ports(int cell) { Check(cell); return ports[cell]; }
        public bool IsPowered(int cell) { Check(cell); return powered[cell]; }
        public bool CanRotate(int cell) { Check(cell); return cell != Burned && ports[cell] != 0; }
        public bool Rotate(int cell)
        {
            if (!CanRotate(cell)) return false;
            ports[cell] = ((ports[cell] << 1) | (ports[cell] >> 3)) & 15;
            Trace(); return true;
        }
        private void Trace()
        {
            Array.Clear(powered, 0, powered.Length);
            if ((ports[Input] & W) == 0) return;
            var queue = new Queue<int>(); powered[Input] = true; queue.Enqueue(Input);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(), row = cell / Size, col = cell % Size;
                for (int direction = 0; direction < 4; direction++)
                {
                    if ((ports[cell] & (1 << direction)) == 0) continue;
                    int nr = row + (direction == 0 ? -1 : direction == 2 ? 1 : 0);
                    int nc = col + (direction == 1 ? 1 : direction == 3 ? -1 : 0);
                    if (nr < 0 || nr >= Size || nc < 0 || nc >= Size) continue;
                    int next = nr * Size + nc;
                    if (next == Burned || powered[next] || (ports[next] & (1 << ((direction + 2) % 4))) == 0) continue;
                    powered[next] = true; queue.Enqueue(next);
                }
            }
        }
        private static void Check(int cell)
        { if (cell < 0 || cell >= Size * Size) throw new ArgumentOutOfRangeException(nameof(cell)); }
    }
}
