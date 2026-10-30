using System;
using System.Collections.Generic;
using System.Linq;

namespace Motel.Editor
{
    public static class MotelBlockoutValidation
    {
        private const float Radius = 0.33f; // Prototype radius 0.30 + skin width 0.03.
        private const float Step = 0.2f;
        private const float MinX = -2, MinZ = -25;
        private const int Columns = 151, Rows = 166;

        public static string Validate(MotelBlockoutPlan p)
        {
            Require(MotelBlockoutPlan.CorridorWidth >= 2 * Radius + 0.5f, "Corridor too narrow");
            Require(MotelBlockoutPlan.DoorWidth >= 2 * Radius + 0.2f, "Doorway too narrow");
            Require(MotelBlockoutPlan.Height > 1.8f && MotelBlockoutPlan.DoorHeight > 1.8f, "Headroom");
            Require(p.Doors.Count == 14 && p.Doors.Count(d => d.Operable) == 10, "Door count");
            Require(p.Doors.Any(d => d.From == "Room7" && d.To == "Room8" && d.Operable), "Connection 7-8");
            foreach (var box in p.Boxes)
                Require(box.Width > 0 && box.Height > 0 && box.Depth > 0, "Invalid box: " + box.Name);
            Require(!Blocked(p, p.SpawnX, p.SpawnZ, false, false, true), "Spawn overlaps a solid collider");

            bool[] normal = Flood(p, false, false, false);
            foreach (var zone in p.Zones)
                Require(At(normal, zone.TargetX, zone.TargetZ) == zone.Reachable, "Unexpected reachability: " + zone.Id);

            // Physically remove office and technical from the navigation domain, not just from an abstract graph.
            bool[] evacuation = Flood(p, true, false, false);
            foreach (string id in new[] { "L03", "L05", "L01", "Room2", "Room4", "Room7", "Room8" })
            {
                var zone = p.Zones.Single(z => z.Id == id);
                Require(At(evacuation, zone.TargetX, zone.TargetZ), "Evacuation depends on office/technical: " + id);
            }
            var room8 = p.Zones.Single(z => z.Id == "Room8");
            Require(At(Flood(p, false, true, false), room8.TargetX, room8.TargetZ), "8 unreachable through 7");
            Require(!At(Flood(p, false, true, true), room8.TargetX, room8.TargetZ), "8 has an unintended opening");
            CheckDoorSweeps(p);
            return "PASS: 14 doors (10 operable), 17 zones; 0.2 m navigation grid with 0.33 m radius; " +
                "spawn, locked rooms, both exits/parking/road, evacuation without office/technical, " +
                "7-8 fallback and door sweeps (5 degree steps). Play Mode is still required.";
        }

        private static bool[] Flood(MotelBlockoutPlan p, bool excludeServices, bool close8, bool closeConnection)
        {
            var blocked = new bool[Columns * Rows];
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Columns; col++)
                {
                    float x = MinX + col * Step, z = MinZ + row * Step;
                    blocked[row * Columns + col] = Blocked(p, x, z, close8, closeConnection, false) ||
                        (excludeServices && p.Zones.Any(a => (a.Id == "L04" || a.Id == "L06") &&
                            x > a.MinX - Radius && x < a.MaxX + Radius && z > a.MinZ - Radius && z < a.MaxZ + Radius));
                }

            var visited = new bool[blocked.Length];
            int start = Index(p.SpawnX, p.SpawnZ);
            Require(!blocked[start], "Spawn not navigable");
            var queue = new Queue<int>();
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue(), col = at % Columns, row = at / Columns;
                if (col > 0) Visit(at - 1, blocked, visited, queue);
                if (col < Columns - 1) Visit(at + 1, blocked, visited, queue);
                if (row > 0) Visit(at - Columns, blocked, visited, queue);
                if (row < Rows - 1) Visit(at + Columns, blocked, visited, queue);
            }
            return visited;
        }

        private static void Visit(int index, bool[] blocked, bool[] visited, Queue<int> queue)
        {
            if (blocked[index] || visited[index]) return;
            visited[index] = true;
            queue.Enqueue(index);
        }

        private static int Index(float x, float z)
        {
            int col = (int)Math.Round((x - MinX) / Step), row = (int)Math.Round((z - MinZ) / Step);
            Require(col >= 0 && col < Columns && row >= 0 && row < Rows, "Target outside grid");
            return row * Columns + col;
        }

        private static bool At(bool[] grid, float x, float z) => grid[Index(x, z)];

        private static bool BlocksBody(MotelBlockoutPlan.Box b) => b.Solid && b.Y + b.Height / 2 > 0.25f && b.Y - b.Height / 2 < 1.8f;

        private static bool Blocked(MotelBlockoutPlan p, float x, float z, bool close8, bool closeConnection, bool allClosed)
        {
            foreach (var box in p.Boxes)
            {
                if (!BlocksBody(box)) continue;
                double dx = Math.Max(Math.Abs(x - box.X) - box.Width / 2, 0);
                double dz = Math.Max(Math.Abs(z - box.Z) - box.Depth / 2, 0);
                if (dx * dx + dz * dz <= Radius * Radius) return true;
            }
            foreach (var door in p.Doors)
            {
                bool closed = allClosed || !door.Operable || (close8 && door.Name == "Room8") ||
                    (closeConnection && door.Name == "Connection 7-8");
                DoorPose(door, closed ? 0 : -95, out double hx, out double hz, out double cos, out double sin);
                double cx = hx + 0.75 * cos, cz = hz - 0.75 * sin;
                double lx = (x - cx) * cos - (z - cz) * sin;
                double lz = (x - cx) * sin + (z - cz) * cos;
                double dx = Math.Max(Math.Abs(lx) - 0.74, 0), dz = Math.Max(Math.Abs(lz) - 0.06, 0);
                if (dx * dx + dz * dz <= Radius * Radius) return true;
            }
            return false;
        }

        private static void DoorPose(MotelBlockoutPlan.Door door, float angle, out double hx, out double hz, out double cos, out double sin)
        {
            double r = door.Yaw * Math.PI / 180;
            hx = door.X - 0.75 * Math.Cos(r);
            hz = door.Z + 0.75 * Math.Sin(r);
            r = (door.Yaw + angle) * Math.PI / 180;
            cos = Math.Cos(r);
            sin = Math.Sin(r);
        }

        private static void CheckDoorSweeps(MotelBlockoutPlan p)
        {
            foreach (var door in p.Doors.Where(d => d.Operable))
                for (int angle = 0; angle >= -95; angle -= 5)
                {
                    DoorPose(door, angle, out double hx, out double hz, out double cos, out double sin);
                    // Sample the solid panel including both faces, except the small hinge/jamb contact area.
                    for (double along = 0.2; along <= 1.491; along += 0.043)
                        foreach (double across in new[] { -0.06, 0.0, 0.06 })
                        {
                            double x = hx + along * cos + across * sin, z = hz - along * sin + across * cos;
                            foreach (var box in p.Boxes.Where(BlocksBody))
                                Require(!(Math.Abs(x - box.X) < box.Width / 2 - 0.002 && Math.Abs(z - box.Z) < box.Depth / 2 - 0.002),
                                    "Door sweep intersects " + box.Name + ": " + door.Name + " at " + angle);
                        }
                }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Motel layout: " + message);
        }
    }
}
