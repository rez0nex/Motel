using System;
using Motel;

namespace Motel.Editor
{
    public static class BypassCircuitChecks
    {
        public static void Solve(BypassCircuit board)
        {
            // Independent reference route: B1 → A1 → A2 → A3 → B3 → C3 → C4 → B4.
            int[] cells = { 0, 1, 2, 6, 10, 11, 7 }, turns = { 1, 1, 2, 1, 2, 2, 2 };
            for (int n = 0; n < cells.Length; n++) for (int t = 0; t < turns[n]; t++) board.Rotate(cells[n]);
        }
        public static string Validate()
        {
            var board = new BypassCircuit();
            int[] initial = { 3, 5, 3, 10, 9, 0, 10, 9, 0, 10, 12, 6, 0, 0, 0, 0 };
            for (int i = 0; i < 16; i++)
            {
                Require(board.Ports(i) == initial[i], "Wrong initial ports " + i);
                Require(board.IsPowered(i) == (i == 4), "Initial signal leaks " + i);
                if (initial[i] == 0) Require(!board.Rotate(i), "Burned/empty section rotates");
            }
            Require(!board.IsComplete, "Initial board solved");
            board.Rotate(4); Require(board.Ports(4) == 3, "Rotation is not clockwise");
            for (int i = 0; i < 16; i++) Require(!board.IsPowered(i), "Disconnected input still powers the board");
            for (int i = 0; i < 3; i++) board.Rotate(4);
            Solve(board); Require(board.IsComplete, "Reference route fails");
            int[] route = { 4, 0, 1, 2, 6, 10, 11, 7 };
            for (int i = 0; i < 16; i++) Require(board.IsPowered(i) == (Array.IndexOf(route, i) >= 0), "Incorrect powered set " + i);
            foreach (int cell in route)
            {
                board.Rotate(cell); Require(!board.IsComplete, "Broken route is accepted at " + cell);
                for (int i = 0; i < 3; i++) board.Rotate(cell);
                Require(board.IsComplete, "Restoring a section failed");
            }
            // Power can reach B4 without its east-facing output: that must not enable Start.
            board.Rotate(7); Require(board.IsPowered(7) && !board.IsComplete, "Output port ignored");
            for (int i = 0; i < 3; i++) board.Rotate(7);
            for (int a = 0; a < 4; a++)
            {
                for (int b = 0; b < 4; b++) { board.Rotate(9); Require(board.IsComplete && !board.IsPowered(9) && !board.IsPowered(3), "Distractors required/leak"); }
                board.Rotate(3);
            }
            var reset = new BypassCircuit();
            for (int i = 0; i < 16; i++) Require(reset.Ports(i) == initial[i], "Reset board not initial");
            return "PASS: 4x4 fixed ports, clockwise rotation, reciprocal signal, broken/input/output gates, burned/empty cells, exact reference route, optional distractors and fresh board.";
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
