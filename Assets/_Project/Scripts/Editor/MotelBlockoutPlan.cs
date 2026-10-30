using System;
using System.Collections.Generic;

namespace Motel.Editor
{
    // Pure layout data: the Unity generator and the standalone geometry check use the same plan.
    public sealed class MotelBlockoutPlan
    {
        public const float Height = 2.8f;
        public const float WallThickness = 0.16f;
        public const float DoorWidth = 1.5f;
        public const float DoorHeight = 2.3f;
        public const float CorridorWidth = 2.4f;
        public readonly List<Box> Boxes = new List<Box>();
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<Sign> Signs = new List<Sign>();
        public readonly List<Zone> Zones = new List<Zone>();
        public readonly float SpawnX = 1.7f, SpawnZ = -15.1f;

        public sealed class Box
        {
            public string Name, Group, Material;
            public float X, Y, Z, Width, Height, Depth;
            public bool Solid;
        }

        public sealed class Door
        {
            public string Name, From, To;
            public float X, Z, Yaw;
            public bool Operable;
        }

        public sealed class Sign
        {
            public string Text;
            public float X, Y, Z, Yaw;
        }

        public sealed class Zone
        {
            public string Id;
            public float MinX, MaxX, MinZ, MaxZ, TargetX, TargetZ;
            public bool Reachable;
        }

        public static MotelBlockoutPlan Create()
        {
            var p = new MotelBlockoutPlan();
            p.Shell();
            p.GuestRooms();
            p.ServiceWing();
            p.Outside();
            return p;
        }

        private void Shell()
        {
            Add("Ground", "Outside", "Ground", 10, -0.17f, -7, 42, 0.26f, 40);
            Add("Residential Floor", "Floors", "Floor", 11.4f, -0.1f, 0, 22.96f, 0.2f, 12.96f);
            Add("Service Floor", "Floors", "Floor", 3, -0.1f, -11.2f, 6.16f, 0.2f, 9.6f);
            Add("North Ceiling", "Ceilings", "Ceiling", 11.4f, 2.9f, 3.8f, 22.96f, 0.2f, 5.2f);
            Add("Corridor Ceiling", "Ceilings", "Ceiling", 11.4f, 2.9f, 0, 22.96f, 0.2f, 2.4f);
            Add("South Ceiling", "Ceilings", "Ceiling", 14.4f, 2.9f, -3.8f, 16.8f, 0.2f, 5.2f);
            Add("Service Ceiling", "Ceilings", "Ceiling", 3, 2.9f, -8.6f, 6.16f, 0.2f, 14.8f);
            WallX("North Exterior", 0, 22.8f, 6.4f);
            WallX("South Exterior", 6, 22.8f, -6.4f);
            WallX("Reception Back", 0, 6, -16);
            WallZ("West Exterior", 0, -16, 6.4f);
            WallZ("East Exterior", 22.8f, -6.4f, 6.4f, 0);
            WallZ("Service East", 6, -16, -1.2f, -13.6f);
            DoorAt("Emergency Exit", "L03", "L05", 22.8f, 0, 90, true, "Запасной выход");
            DoorAt("Main Entrance", "L02", "L01", 6, -13.6f, 90, true, "Главный вход");
            ZoneAt("L03", 0, 22.8f, -1.2f, 1.2f, 11.4f, 0);
            ZoneAt("L13", 3.6f, 6, -10.8f, -1.2f, 4.8f, -6);
            ZoneAt("L02", 0, 6, -16, -10.8f, 4.8f, -12);
        }

        private void GuestRooms()
        {
            var northDoors = new float[4];
            var southDoors = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float northLeft = i * 5.7f;
                float southLeft = 6 + i * 4.2f;
                northDoors[i] = northLeft + 2.85f;
                southDoors[i] = southLeft + 2.1f;
                bool northOpen = i == 1 || i == 3;
                bool southOpen = i >= 2;
                string northId = "Room" + (i + 1);
                string southId = "Room" + (i + 5);
                DoorAt(northId, "L03", northId, northDoors[i], 1.2f, 0, northOpen, (i + 1).ToString());
                DoorAt(southId, "L03", southId, southDoors[i], -1.2f, 180, southOpen,
                    i == 3 ? "Подсобка" : (i + 5).ToString());
                ZoneAt(northId, northLeft, northLeft + 5.7f, 1.2f, 6.4f, northLeft + 3.2f, 3, northOpen);
                ZoneAt(southId, southLeft, southLeft + 4.2f, -6.4f, -1.2f, southLeft + 2.5f, -2.6f, southOpen);
                if (i > 0)
                {
                    WallZ("North Divider " + i, northLeft, 1.2f, 6.4f);
                    if (i == 3)
                        WallZ("Divider 7-8", southLeft, -6.4f, -1.2f, -2.8f);
                    else
                        WallZ("South Divider " + i, southLeft, -6.4f, -1.2f);
                }
                if (northOpen) Bed(northId, northLeft + 1.3f, 4.8f);
                if (southOpen) Bed(southId, southLeft + (i == 3 ? 2.9f : 1.1f), -4.8f, i == 3 ? -1 : 1);
            }
            WallX("North Corridor", 0, 22.8f, 1.2f, northDoors);
            WallX("South Corridor", 6, 22.8f, -1.2f, southDoors);
            DoorAt("Connection 7-8", "Room7", "Room8", 18.6f, -2.8f, 90, true, "7 ↔ 8");
        }

        private void ServiceWing()
        {
            WallX("Office North", 0, 3.6f, -1.2f);
            WallX("Office Linen Divider", 0, 3.6f, -4.4f);
            WallX("Linen Technical Divider", 0, 3.6f, -7.6f);
            WallX("Technical Reception Divider", 0, 3.6f, -10.8f);
            WallZ("Service Doors Wall", 3.6f, -10.8f, -1.2f, -9.2f, -6, -2.8f);
            DoorAt("Office", "L13", "L04", 3.6f, -2.8f, -90, true, "Кабинет");
            DoorAt("Linen", "L13", "L11", 3.6f, -6, -90, true, "Бельевая");
            DoorAt("Technical", "L13", "L06", 3.6f, -9.2f, -90, true, "Техническая");
            ZoneAt("L04", 0, 3.6f, -4.4f, -1.2f, 2.5f, -2.3f);
            ZoneAt("L11", 0, 3.6f, -7.6f, -4.4f, 2.5f, -5.5f);
            ZoneAt("L06", 0, 3.6f, -10.8f, -7.6f, 2.5f, -8.7f);
            Add("Office Desk", "Furniture", "Furniture", 1.1f, 0.45f, -2.8f, 1.5f, 0.9f, 1);
            Add("Linen Shelves", "Furniture", "Furniture", 0.65f, 0.9f, -6, 0.8f, 1.8f, 2);
            Add("Technical Equipment", "Furniture", "Equipment", 1.1f, 0.85f, -9.3f, 1.5f, 1.7f, 1.2f);
            Add("Equipment Panel", "Furniture", "Marker", 1.86f, 1.3f, -9.3f, 0.04f, 0.4f, 0.6f, false);
            Add("Reception Counter", "Furniture", "Furniture", 2.1f, 0.5f, -13.8f, 3.2f, 1, 0.9f);
        }

        private void Outside()
        {
            Add("Parking Surface", "Outside", "Asphalt", 15.5f, -0.025f, -14.5f, 17, 0.03f, 10, false);
            Add("Road", "Outside", "Asphalt", 12, -0.025f, -23, 34, 0.03f, 4, false);
            Add("Emergency Walkway", "Outside", "Paving", 24.6f, -0.025f, -8.7f, 3.2f, 0.03f, 19.4f, false);
            Add("Main Walkway", "Outside", "Paving", 7.1f, -0.025f, -13.6f, 2.2f, 0.03f, 3, false);
            for (int i = 0; i <= 4; i++)
                Add("Parking Stripe " + i, "Outside", "Paint", 8.5f + i * 3.5f, 0.002f, -13.6f, 0.08f, 0.012f, 5, false);
            Add("Parking Back Stripe", "Outside", "Paint", 15.5f, 0.002f, -11.1f, 14, 0.012f, 0.08f, false);
            Add("Assembly Marker", "Outside", "Marker", 15, 0.005f, -18.1f, 3, 0.02f, 2, false);
            Add("Assembly Sign Post", "Outside", "Equipment", 16.9f, 1, -18.8f, 0.08f, 2, 0.08f);
            ZoneAt("L01", 7, 24, -19.5f, -9.5f, 15, -18.1f);
            ZoneAt("L05", 22.9f, 26.2f, -19, 1.2f, 25, 0);
            ZoneAt("L12", -5, 29, -25, -21, 15, -23);
        }

        private void Bed(string room, float x, float z, float tableSide = 1)
        {
            Add(room + " Bed", "Furniture", "Furniture", x, 0.3f, z, 1.4f, 0.6f, 2);
            Add(room + " Mattress", "Furniture", "Ceiling", x, 0.65f, z, 1.35f, 0.1f, 1.95f);
            Add(room + " Nightstand", "Furniture", "Furniture", x + 1.1f * tableSide, 0.3f, z + 0.7f, 0.5f, 0.6f, 0.5f);
        }

        private void WallX(string name, float start, float end, float z, params float[] openings)
        {
            Wall(name, start, end, z, false, openings);
        }

        private void WallZ(string name, float x, float start, float end, params float[] openings)
        {
            Wall(name, start, end, x, true, openings);
        }

        private void Wall(string name, float start, float end, float fixedAxis, bool alongZ, float[] openings)
        {
            Array.Sort(openings);
            float cursor = start;
            for (int i = 0; i <= openings.Length; i++)
            {
                float stop = i < openings.Length ? openings[i] - DoorWidth / 2 : end;
                if (stop < cursor) throw new InvalidOperationException("Overlapping wall openings: " + name);
                if (stop > cursor)
                    WallBox(name + " Segment " + i, (cursor + stop) / 2, fixedAxis, stop - cursor, Height / 2, Height, alongZ);
                if (i < openings.Length)
                {
                    WallBox(name + " Header " + i, openings[i], fixedAxis, DoorWidth,
                        (Height + DoorHeight) / 2, Height - DoorHeight, alongZ);
                    cursor = openings[i] + DoorWidth / 2;
                }
            }
        }

        private void WallBox(string name, float center, float fixedAxis, float length, float y, float height, bool alongZ)
        {
            Add(name, "Walls", "Wall", alongZ ? fixedAxis : center, y, alongZ ? center : fixedAxis,
                alongZ ? WallThickness : length, height, alongZ ? length : WallThickness);
        }

        private void DoorAt(string name, string from, string to, float x, float z, float yaw, bool operable, string label)
        {
            Doors.Add(new Door { Name = name, From = from, To = to, X = x, Z = z, Yaw = yaw, Operable = operable });
            double r = yaw * Math.PI / 180;
            Signs.Add(new Sign { Text = label, X = x - (float)Math.Sin(r) * 0.12f,
                Y = 2.46f, Z = z - (float)Math.Cos(r) * 0.12f, Yaw = yaw });
        }

        private void ZoneAt(string id, float minX, float maxX, float minZ, float maxZ, float x, float z, bool reachable = true)
        {
            Zones.Add(new Zone { Id = id, MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ,
                TargetX = x, TargetZ = z, Reachable = reachable });
        }

        private void Add(string name, string group, string material, float x, float y, float z,
            float width, float height, float depth, bool solid = true)
        {
            Boxes.Add(new Box { Name = name, Group = group, Material = material, X = x, Y = y, Z = z,
                Width = width, Height = height, Depth = depth, Solid = solid });
        }
    }
}
