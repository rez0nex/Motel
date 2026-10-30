using System;

namespace Motel.Editor
{
    // u goes right on the plan, v goes away from the corridor. Only v is reflected.
    public static class MotelGuestRoomModule
    {
        public const float Width = 5, Depth = 6.5f, VestibuleWidth = 2, FrontDepth = 2.3f;
        public const float BathroomLeft = 2.16f, LivingStart = 2.46f, ConnectionDepth = 4.25f;
        public const float EntranceWidth = 1.15f, BathroomDoorWidth = 1.1f, LivingOpeningWidth = 1.2f;

        public sealed class Layout
        {
            public int Number, Direction;
            public float Left, Front;
            public bool Accessible;
            public string Id => "Room" + Number;
            public float X(float u) => Left + MotelBlockoutPlan.WallThickness / 2 + u;
            public float Z(float v) => Front + Direction * (MotelBlockoutPlan.WallThickness / 2 + v);
        }

        public static void Build(MotelBlockoutPlan p, int number, float left, float front, int direction, bool accessible)
        {
            var r = new Layout { Number = number, Left = left, Front = front, Direction = direction, Accessible = accessible };
            p.Rooms.Add(r);
            p.WallWithOpenings(r.Id + " Front", left, left + MotelBlockoutPlan.RoomPitch, front, false,
                new MotelBlockoutPlan.Opening(r.X(0.8f), EntranceWidth));
            p.WallWithOpenings(r.Id + " Living Partition", left, left + MotelBlockoutPlan.RoomPitch, r.Z(2.38f), false,
                new MotelBlockoutPlan.Opening(r.X(1.15f), LivingOpeningWidth));
            p.WallWithOpenings(r.Id + " Bathroom Divider", Math.Min(front, r.Z(2.38f)), Math.Max(front, r.Z(2.38f)), r.X(2.08f), true,
                new MotelBlockoutPlan.Opening(r.Z(1.4f), BathroomDoorWidth));
            p.DoorAt(r.Id, "L03", r.Id + ".Vestibule", r.X(0.8f), front, 0, accessible,
                number == 8 ? "Подсобка" : number.ToString(), EntranceWidth, -95 * direction, direction > 0 ? 0 : 180);
            p.DoorAt(r.Id + " Bathroom", r.Id + ".Vestibule", r.Id + ".Bathroom", r.X(2.08f), r.Z(1.4f), 90 * direction,
                true, "Санузел", BathroomDoorWidth, -95 * direction, 90);
            Zone(p, r, r.Id, 0, Width, 0, Depth, 1.6f, 4.3f);
            Zone(p, r, r.Id + ".Vestibule", 0, VestibuleWidth, 0, FrontDepth, 1.3f, 1.15f);
            Zone(p, r, r.Id + ".Bathroom", BathroomLeft, Width, 0, FrontDepth, 3.4f, 1.4f);
            Zone(p, r, r.Id + ".Living", 0, Width, LivingStart, Depth, 1.6f, 4.3f);

            Box(p,r,"Bed","Furniture",3.5f,0.24f,5.55f,2,0.48f,1.6f);
            Box(p,r,"Mattress","Ceiling",3.5f,0.54f,5.55f,2,0.12f,1.6f);
            Box(p,r,"Headboard","Furniture",4.47f,0.65f,5.55f,0.06f,1.3f,1.6f);
            Box(p,r,"Pillow A","Ceiling",4.15f,0.65f,5.18f,0.45f,0.1f,0.55f,false);
            Box(p,r,"Pillow B","Ceiling",4.15f,0.65f,5.92f,0.45f,0.1f,0.55f,false);
            Box(p,r,"Nightstand","Furniture",4.76f,0.25f,5.55f,0.42f,0.5f,0.5f);
            Box(p,r,"Wardrobe","Furniture",number == 8 ? 4.55f : 0.4f,0.95f,3.4f,0.6f,1.9f,1.2f);
            Box(p,r,"Desk","Furniture",0.4f,0.375f,5.7f,0.6f,0.75f,1.2f);
            Box(p,r,"Shower Tray","Paving",4.45f,0.03f,1.75f,1,0.06f,1);
            Box(p,r,"Shower Back","Ceiling",4.45f,1,2.23f,1,2,0.04f);
            Box(p,r,"Shower Side","Ceiling",4.93f,1,1.75f,0.04f,2,1);
            Box(p,r,"Sink","Ceiling",4.6f,0.8f,0.45f,0.6f,0.16f,0.45f);
            Box(p,r,"Sink Pedestal","Ceiling",4.6f,0.35f,0.45f,0.22f,0.7f,0.22f);
            Box(p,r,"Toilet Bowl","Ceiling",3.4f,0.23f,0.45f,0.45f,0.46f,0.65f);
            Box(p,r,"Toilet Tank","Ceiling",3.4f,0.55f,0.15f,0.45f,0.7f,0.2f);
        }

        private static void Box(MotelBlockoutPlan p, Layout r, string name, string material, float u, float y, float v,
            float width, float height, float depth, bool solid = true)
            => p.Add(r.Id + " " + name, "Furniture", material, r.X(u), y, r.Z(v), width, height, depth, solid);

        private static void Zone(MotelBlockoutPlan p, Layout r, string name, float minU, float maxU, float minV, float maxV, float u, float v)
            => p.ZoneAt(name, r.X(minU), r.X(maxU), Math.Min(r.Z(minV),r.Z(maxV)), Math.Max(r.Z(minV),r.Z(maxV)), r.X(u),r.Z(v),r.Accessible);
    }
}
