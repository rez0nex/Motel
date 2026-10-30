using System;
using System.Collections.Generic;
using System.Linq;

namespace Motel.Editor
{
    public sealed class MotelBlockoutPlan
    {
        public const float Height = 2.8f, WallThickness = 0.16f, DoorWidth = 1.5f, DoorHeight = 2.3f;
        public const float CorridorWidth = 2.4f, GuestStart = 6, RoomPitch = 5.16f, WingEnd = GuestStart + 4 * RoomPitch;
        public const float OuterZ = 1.2f + 6.5f + WallThickness;
        public readonly List<Box> Boxes = new List<Box>();
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<Sign> Signs = new List<Sign>();
        public readonly List<Zone> Zones = new List<Zone>();
        public readonly List<MotelGuestRoomModule.Layout> Rooms = new List<MotelGuestRoomModule.Layout>();
        public readonly List<Window> Windows = new List<Window>();
        public readonly float SpawnX = 1.55f, SpawnZ = -13.6f, SpawnYaw = 90;

        public sealed class Box
        {
            public string Name, Group, Material;
            public float X, Y, Z, Width, Height, Depth;
            public bool Solid;
        }
        public sealed class Door
        {
            public string Name, From, To;
            public float X, Z, Yaw, Width = DoorWidth, OpenAngle = -95;
            public bool Operable;
        }
        public sealed class Sign
        {
            public string Id, Text;
            public float X, Y, Z, Yaw;
        }
        public sealed class Zone
        {
            public string Id;
            public float MinX, MaxX, MinZ, MaxZ, TargetX, TargetZ;
            public bool Reachable;
        }
        public sealed class Opening
        {
            public float Center, Width;
            public Opening(float center, float width) { Center = center; Width = width; }
        }
        public sealed class Window
        {
            public string Name;
            public float X, Z, Width;
            public const float Sill = 1.1f, Top = 2.2f;
        }

        public static MotelBlockoutPlan Create()
        {
            var p = new MotelBlockoutPlan();
            p.Shell(); p.GuestRooms(); p.ServiceWing(); p.Outside();
            return p;
        }

        private void Shell()
        {
            Add("Ground", "Outside", "Ground", 14, -0.17f, -7, 44, 0.26f, 44);
            Add("Residential Floor", "Floors", "Floor", (GuestStart + WingEnd) / 2, -0.1f, 0, WingEnd - GuestStart + 0.16f, 0.2f, OuterZ * 2 + 0.16f);
            Add("Corridor Turn Floor", "Floors", "Floor", 4.8f, -0.1f, 0, 2.4f, 0.2f, 2.56f);
            Add("Service Floor", "Floors", "Floor", 3, -0.1f, -8.6f, 6.16f, 0.2f, 14.8f);
            float roomDepth = OuterZ - 1.2f;
            foreach (int direction in new[] { 1, -1 })
                Add(direction > 0 ? "North Ceiling" : "South Ceiling", "Ceilings", "Ceiling",
                    (GuestStart + WingEnd) / 2, 2.9f, direction * (OuterZ + 1.2f) / 2, WingEnd - GuestStart + 0.16f, 0.2f, roomDepth);
            Add("Corridor Ceiling", "Ceilings", "Ceiling", (3.6f + WingEnd) / 2, 2.9f, 0, WingEnd - 3.6f, 0.2f, 2.4f);
            Add("Service Ceiling", "Ceilings", "Ceiling", 3, 2.9f, -8.6f, 6.16f, 0.2f, 14.8f);
            float[] roomWindows = Enumerable.Range(0, 4).Select(i => GuestStart + i * RoomPitch + 0.08f + 1.6f).ToArray();
            WallWithWindows("North Exterior", GuestStart, WingEnd, OuterZ, roomWindows);
            WallWithWindows("South Exterior", GuestStart, WingEnd, -OuterZ, roomWindows);
            WallWithWindows("Reception Back", 0, 6, -16, new[] { 4.7f });
            WallZ("Service West Exterior", 0, -16, -1.2f);
            WallZ("Corridor West", 3.6f, -1.2f, 1.2f);
            WallX("Corridor Turn North", 3.6f, GuestStart, 1.2f);
            WallZ("North West Exterior", GuestStart, 1.2f, OuterZ);
            WallZ("East Exterior", WingEnd, -OuterZ, OuterZ, 0);
            WallZ("Service East", 6, -16, -1.2f, -13.6f);
            DoorAt("Emergency Exit", "L03", "L05", WingEnd, 0, 90, true, "Запасной выход");
            DoorAt("Main Entrance", "L02", "L01", 6, -13.6f, 90, true, "Главный вход");
            ZoneAt("L03", 3.6f, WingEnd, -1.2f, 1.2f, 13, 0);
            ZoneAt("L13", 3.6f, 6, -10.8f, -1.2f, 4.8f, -6);
            ZoneAt("L02", 0, 6, -16, -10.8f, 4.8f, -12);
        }

        private void GuestRooms()
        {
            for (int i = 0; i < 4; i++)
            {
                float left = GuestStart + i * RoomPitch;
                MotelGuestRoomModule.Build(this, i + 1, left, 1.2f, 1, i == 1 || i == 3);
                MotelGuestRoomModule.Build(this, i + 5, left, -1.2f, -1, i >= 2);
                if (i == 0) continue;
                WallZ("North Divider " + i, left, 1.2f, OuterZ);
                if (i == 3)
                {
                    var seven = Rooms.Single(r => r.Number == 7);
                    float connectionZ = seven.Z(MotelGuestRoomModule.ConnectionDepth);
                    WallWithOpenings("Divider 7-8", -OuterZ, -1.2f, left, true, new Opening(connectionZ, 1.1f));
                    DoorAt("Connection 7-8", "Room7.Living", "Room8.Living", left, connectionZ, 90, true, "7 ↔ 8", 1.1f);
                }
                else WallZ("South Divider " + i, left, -OuterZ, -1.2f);
            }
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
            Add("Reception Counter", "Furniture", "Furniture", 3, 0.5f, -13.6f, 0.9f, 1, 2.8f);
            Add("Reception Countertop", "Furniture", "Furniture", 3, 1.025f, -13.6f, 1, 0.05f, 2.9f);
            Add("Reception Front Panel", "Furniture", "Equipment", 3.46f, 0.55f, -13.6f, 0.02f, 0.6f, 2.55f, false);
            Add("Reception Monitor", "Furniture", "Equipment", 2.75f, 1.25f, -13.6f, 0.1f, 0.4f, 0.55f, false);
        }

        private void Outside()
        {
            Add("Parking Surface", "Outside", "Asphalt", 16.5f, -0.025f, -15.5f, 19, 0.03f, 10, false);
            Add("Road", "Outside", "Asphalt", 15, -0.025f, -23, 36, 0.03f, 4, false);
            Add("Emergency Walkway", "Outside", "Paving", WingEnd + 1.7f, -0.025f, -8.7f, 3.2f, 0.03f, 19.4f, false);
            Add("Main Walkway", "Outside", "Paving", 7.1f, -0.025f, -13.6f, 2.2f, 0.03f, 3, false);
            for (int i = 0; i <= 4; i++)
                Add("Parking Stripe " + i, "Outside", "Paint", 8.5f + i * 4, 0.002f, -13.6f, 0.08f, 0.012f, 5, false);
            Add("Parking Back Stripe", "Outside", "Paint", 16.5f, 0.002f, -11.1f, 16, 0.012f, 0.08f, false);
            Add("Assembly Marker", "Outside", "Marker", 18, 0.005f, -18.1f, 3, 0.02f, 2, false);
            Add("Assembly Sign Post", "Outside", "Equipment", 19.9f, 1, -18.8f, 0.08f, 2, 0.08f);
            ZoneAt("L01", 7, 26, -20.5f, -10.5f, 18, -18.1f);
            ZoneAt("L05", WingEnd + 0.1f, WingEnd + 3.4f, -19, 1.2f, WingEnd + 2, 0);
            ZoneAt("L12", -3, 33, -25, -21, 18, -23);
        }

        private void WallWithWindows(string name, float start, float end, float z, float[] centers)
        {
            const float width = 1.6f, rim = 0.055f;
            float cursor = start;
            for (int i = 0; i < centers.Length; i++)
            {
                float x = centers[i], left = x - width / 2, right = x + width / 2;
                WallBox(name + " Segment " + i, (cursor + left) / 2, z, left - cursor, Height / 2, Height, false);
                WallBox(name + " Sill " + i, x, z, width, Window.Sill / 2, Window.Sill, false);
                WallBox(name + " Lintel " + i, x, z, width, (Window.Top + Height) / 2, Height - Window.Top, false);
                string id = name + " Window " + i;
                Windows.Add(new Window { Name = id, X = x, Z = z, Width = width });
                Add(id + " Glass", "Windows", "Glass", x, (Window.Sill + Window.Top) / 2, z,
                    width - rim * 2, Window.Top - Window.Sill - rim * 2, 0.035f);
                foreach (int side in new[] { -1, 1 })
                {
                    Add(id + " Vertical " + side, "Windows", "Equipment", x + side * (width - rim) / 2,
                        (Window.Sill + Window.Top) / 2, z, rim, Window.Top - Window.Sill, 0.18f);
                    Add(id + " Horizontal " + side, "Windows", "Equipment", x,
                        (Window.Sill + Window.Top) / 2 + side * (Window.Top - Window.Sill - rim) / 2,
                        z, width, rim, 0.18f);
                }
                Add(id + " Mullion", "Windows", "Equipment", x, (Window.Sill + Window.Top) / 2,
                    z, rim, Window.Top - Window.Sill, 0.12f);
                cursor = right;
            }
            WallBox(name + " Segment " + centers.Length, (cursor + end) / 2, z, end - cursor, Height / 2, Height, false);
        }

        internal void WallX(string name, float start, float end, float z, params float[] openings)
            => WallWithOpenings(name, start, end, z, false, openings.Select(c => new Opening(c, DoorWidth)).ToArray());
        internal void WallZ(string name, float x, float start, float end, params float[] openings)
            => WallWithOpenings(name, start, end, x, true, openings.Select(c => new Opening(c, DoorWidth)).ToArray());

        internal void WallWithOpenings(string name, float start, float end, float fixedAxis, bool alongZ, params Opening[] openings)
        {
            Array.Sort(openings, (a,b) => a.Center.CompareTo(b.Center));
            float cursor = start;
            for (int i = 0; i <= openings.Length; i++)
            {
                float stop = i < openings.Length ? openings[i].Center - openings[i].Width / 2 : end;
                if (stop < cursor - 0.001f) throw new InvalidOperationException("Overlapping wall openings: " + name);
                if (stop > cursor) WallBox(name + " Segment " + i, (cursor + stop) / 2, fixedAxis, stop - cursor, Height / 2, Height, alongZ);
                if (i < openings.Length)
                {
                    WallBox(name + " Header " + i, openings[i].Center, fixedAxis, openings[i].Width,
                        (Height + DoorHeight) / 2, Height - DoorHeight, alongZ);
                    cursor = openings[i].Center + openings[i].Width / 2;
                }
            }
        }

        private void WallBox(string name, float center, float fixedAxis, float length, float y, float height, bool alongZ)
            => Add(name, "Walls", "Wall", alongZ ? fixedAxis : center, y, alongZ ? center : fixedAxis,
                alongZ ? WallThickness : length, height, alongZ ? length : WallThickness);

        internal void DoorAt(string name, string from, string to, float x, float z, float yaw, bool operable, string label,
            float width = DoorWidth, float angle = -95, float labelYaw = float.NaN)
        {
            Doors.Add(new Door { Name = name, From = from, To = to, X = x, Z = z, Yaw = yaw, Operable = operable, Width = width, OpenAngle = angle });
            if (float.IsNaN(labelYaw)) labelYaw = yaw;
            double r = labelYaw * Math.PI / 180;
            Signs.Add(new Sign { Id = name, Text = label, X = x - (float)Math.Sin(r) * 0.12f,
                Y = 2.46f, Z = z - (float)Math.Cos(r) * 0.12f, Yaw = labelYaw });
        }

        internal void ZoneAt(string id, float minX, float maxX, float minZ, float maxZ, float x, float z, bool reachable = true)
            => Zones.Add(new Zone { Id = id, MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ, TargetX = x, TargetZ = z, Reachable = reachable });

        internal void Add(string name, string group, string material, float x, float y, float z, float width, float height, float depth, bool solid = true)
            => Boxes.Add(new Box { Name = name, Group = group, Material = material, X = x, Y = y, Z = z, Width = width, Height = height, Depth = depth, Solid = solid });
    }
}
