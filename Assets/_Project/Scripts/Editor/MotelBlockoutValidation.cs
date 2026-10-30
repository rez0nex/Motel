using System;
using System.Collections.Generic;
using System.Linq;

namespace Motel.Editor
{
    public static class MotelBlockoutValidation
    {
        private const float Radius = 0.33f, Step = 0.05f, MinX = -2, MinZ = -25;
        private const int Columns = 701, Rows = 681;

        public static string Validate(MotelBlockoutPlan p)
        {
            Require(p.Rooms.Count == 8 && p.Rooms.Select(r=>r.Number).Distinct().Count() == 8, "Eight separate rooms required");
            Require(p.Doors.Count == 22 && p.Doors.Count(d=>d.Operable) == 18, "Door count");
            Require(p.Boxes.Select(b=>b.Name).Distinct().Count() == p.Boxes.Count, "Duplicate geometry names");
            Require(p.Signs.Count == 22 && p.Signs.Select(s=>s.Id).Distinct().Count() == 22, "Duplicate sign identity");
            foreach (var b in p.Boxes) Require(b.Width > 0 && b.Height > 0 && b.Depth > 0, "Invalid box " + b.Name);
            foreach (var d in p.Doors) Require(d.Width >= 1 && Math.Abs(d.OpenAngle) == 95, "Door size or angle " + d.Name);
            foreach (var r in p.Rooms)
            {
                Require(r.Accessible == new[] {2,4,7,8}.Contains(r.Number), "Accessible rooms changed");
                var bath = p.Doors.Where(d=>d.Name == r.Id + " Bathroom").ToArray();
                Require(bath.Length == 1 && bath[0].From == r.Id + ".Vestibule" && bath[0].To == r.Id + ".Bathroom", "Bathroom entrance " + r.Id);
                var bed = p.Boxes.Single(b=>b.Name == r.Id + " Bed");
                Require(Math.Abs(bed.Width-2)<0.001 && Math.Abs(bed.Depth-1.6f)<0.001, "Real bed dimensions " + r.Id);
                Require(p.Boxes.Single(b=>b.Name == r.Id + " Headboard").X > bed.X, "Headboard must be on the right");
                Require(p.Boxes.Count(b=>b.Name == r.Id + " Shower Tray") == 1, "Exactly one bathroom " + r.Id);
                Require(Math.Abs(r.X(5)-r.X(0)-5)<0.001 && Math.Abs(Math.Abs(r.Z(6.5f)-r.Z(0))-6.5)<0.001, "Room dimensions");
            }
            Require(MotelGuestRoomModule.ConnectionDepth - 0.55f > MotelGuestRoomModule.LivingStart, "7-8 must connect living rooms");
            Require(!Blocked(p,p.SpawnX,p.SpawnZ,Radius,true), "Spawn overlaps a closed door or furniture");
            bool[] normal = Flood(p);
            foreach(var z in p.Zones) Require(At(normal,z.TargetX,z.TargetZ) == z.Reachable, "Unexpected reachability: " + z.Id);
            bool[] evacuation = Flood(p, excludeServices:true);
            foreach(string id in new[] {"L03","L05","L01","Room2","Room4","Room7","Room8"})
                CheckZone(p,evacuation,id,true,"Evacuation depends on office/technical");
            CheckZone(p,Flood(p,closedDoor:"Room8"),"Room8",true,"8 unreachable through 7");
            CheckZone(p,Flood(p,closedDoor:"Room8",closeConnection:true),"Room8",false,"Unintended opening into 8");
            foreach(int n in new[] {2,4,7,8})
            {
                string id = "Room"+n;
                bool[] isolated = Flood(p,closedDoor:id,closeConnection:true);
                CheckZone(p,isolated,id,false,"Rooms must be separate");
                CheckZone(p,isolated,id+".Bathroom",false,"Bathroom leaks into corridor");
                bool[] bathroomClosed = Flood(p,closedDoor:id+" Bathroom");
                CheckZone(p,bathroomClosed,id,true,"Bedroom depends on bathroom");
                CheckZone(p,bathroomClosed,id+".Bathroom",false,"Bathroom has more than one entrance");
            }
            // A separate 1 m diameter probe checks useful route width, without changing the actual player.
            bool[] wide = Flood(p,radius:0.5f);
            foreach(string id in new[] {"L03","L05","L01","Room2","Room4","Room7","Room8"})
                CheckZone(p,wide,id,true,"No approximately 1 m clear main route");
            bool[] wideConnection = Flood(p,closedDoor:"Room8",radius:0.5f);
            CheckZone(p,wideConnection,"Room8",true,"7-8 route narrower than 1 m");
            CheckDoorSweeps(p);
            return "PASS: 8 mirrored suites (5 x 6.5 m inside), 8 separate bathrooms, 22 doors (18 operable), " + p.Zones.Count +
                " zones; 0.05 m grid, original 0.33 m capsule radius and independent 1 m route probe; " +
                "spawn, room/bathroom isolation, both exits, 7-8 fallback, furniture and door sweeps. Play Mode scale review required.";
        }

        private static void CheckZone(MotelBlockoutPlan p,bool[] grid,string id,bool expected,string message)
        {
            var z=p.Zones.Single(a=>a.Id==id);
            Require(At(grid,z.TargetX,z.TargetZ)==expected,message+": "+id);
        }

        private static bool[] Flood(MotelBlockoutPlan p,bool excludeServices=false,string closedDoor=null,bool closeConnection=false,float radius=Radius)
        {
            var blocked=new bool[Columns*Rows];
            foreach(var b in p.Boxes.Where(BlocksBody))
            {
                var box=b;
                Raster(blocked,b.X-b.Width/2-radius,b.X+b.Width/2+radius,b.Z-b.Depth/2-radius,b.Z+b.Depth/2+radius,
                    (x,z)=>CircleBox(box,x,z,radius));
            }
            foreach(var d in p.Doors)
            {
                bool closed=!d.Operable || d.Name==closedDoor || (closeConnection && d.Name=="Connection 7-8");
                var pose=new DoorPose(d,closed?0:d.OpenAngle);
                float extent=d.Width+radius;
                Raster(blocked,d.X-extent,d.X+extent,d.Z-extent,d.Z+extent,(x,z)=>pose.Contains(x,z,radius));
            }
            if(excludeServices)
                foreach(var area in p.Zones.Where(z=>z.Id=="L04" || z.Id=="L06"))
                    Raster(blocked,area.MinX-radius,area.MaxX+radius,area.MinZ-radius,area.MaxZ+radius,(x,z)=>true);
            int start=Index(p.SpawnX,p.SpawnZ);
            Require(!blocked[start],"Spawn not navigable");
            var visited=new bool[blocked.Length];
            var queue=new Queue<int>();
            visited[start]=true; queue.Enqueue(start);
            while(queue.Count>0)
            {
                int at=queue.Dequeue(), col=at%Columns,row=at/Columns;
                if(col>0) Visit(at-1,blocked,visited,queue);
                if(col<Columns-1) Visit(at+1,blocked,visited,queue);
                if(row>0) Visit(at-Columns,blocked,visited,queue);
                if(row<Rows-1) Visit(at+Columns,blocked,visited,queue);
            }
            return visited;
        }

        private static void Raster(bool[] mask,float minX,float maxX,float minZ,float maxZ,Func<float,float,bool> contains)
        {
            int c0=Math.Max(0,(int)Math.Floor((minX-MinX)/Step)),c1=Math.Min(Columns-1,(int)Math.Ceiling((maxX-MinX)/Step));
            int r0=Math.Max(0,(int)Math.Floor((minZ-MinZ)/Step)),r1=Math.Min(Rows-1,(int)Math.Ceiling((maxZ-MinZ)/Step));
            for(int row=r0;row<=r1;row++) for(int col=c0;col<=c1;col++)
                if(contains(MinX+col*Step,MinZ+row*Step)) mask[row*Columns+col]=true;
        }
        private static void Visit(int i,bool[] blocked,bool[] visited,Queue<int> q)
        {
            if(blocked[i] || visited[i]) return;
            visited[i]=true; q.Enqueue(i);
        }
        private static int Index(float x,float z)
        {
            int col=(int)Math.Round((x-MinX)/Step),row=(int)Math.Round((z-MinZ)/Step);
            Require(col>=0 && col<Columns && row>=0 && row<Rows,"Target outside grid");
            return row*Columns+col;
        }
        private static bool At(bool[] grid,float x,float z)=>grid[Index(x,z)];
        private static bool BlocksBody(MotelBlockoutPlan.Box b)=>b.Solid && b.Y+b.Height/2>0.25f && b.Y-b.Height/2<1.8f;
        private static bool CircleBox(MotelBlockoutPlan.Box b,float x,float z,float radius)
        {
            double dx=Math.Max(Math.Abs(x-b.X)-b.Width/2,0),dz=Math.Max(Math.Abs(z-b.Z)-b.Depth/2,0);
            return dx*dx+dz*dz<=radius*radius;
        }
        private static bool Blocked(MotelBlockoutPlan p,float x,float z,float radius,bool allClosed)
        {
            return p.Boxes.Where(BlocksBody).Any(b=>CircleBox(b,x,z,radius)) ||
                p.Doors.Any(d=>new DoorPose(d,allClosed || !d.Operable ? 0:d.OpenAngle).Contains(x,z,radius));
        }
        private sealed class DoorPose
        {
            public readonly double Hx,Hz,Cos,Sin;
            private readonly double halfWidth;
            public DoorPose(MotelBlockoutPlan.Door d,float angle)
            {
                double r=d.Yaw*Math.PI/180;
                Hx=d.X-d.Width/2*Math.Cos(r); Hz=d.Z+d.Width/2*Math.Sin(r);
                r=(d.Yaw+angle)*Math.PI/180; Cos=Math.Cos(r); Sin=Math.Sin(r);
                halfWidth=(d.Width-0.02)/2;
            }
            public bool Contains(double x,double z,double radius)
            {
                double centerOffset=halfWidth+0.01;
                double cx=Hx+centerOffset*Cos,cz=Hz-centerOffset*Sin;
                double lx=(x-cx)*Cos-(z-cz)*Sin,lz=(x-cx)*Sin+(z-cz)*Cos;
                double dx=Math.Max(Math.Abs(lx)-halfWidth,0),dz=Math.Max(Math.Abs(lz)-0.06,0);
                return dx*dx+dz*dz<=radius*radius;
            }
        }
        private static void CheckDoorSweeps(MotelBlockoutPlan p)
        {
            var solids=p.Boxes.Where(BlocksBody).ToArray();
            foreach(var door in p.Doors.Where(d=>d.Operable))
                for(int step=0;step<=19;step++)
                {
                    float angle=door.OpenAngle*step/19;
                    var pose=new DoorPose(door,angle);
                    for(double along=0.2;along<=door.Width-0.009;along+=0.035)
                        foreach(double across in new[] {-0.06,0.0,0.06})
                        {
                            double x=pose.Hx+along*pose.Cos+across*pose.Sin,z=pose.Hz-along*pose.Sin+across*pose.Cos;
                            foreach(var box in solids)
                                Require(!(Math.Abs(x-box.X)<box.Width/2-0.002 && Math.Abs(z-box.Z)<box.Depth/2-0.002),
                                    "Door sweep intersects "+box.Name+": "+door.Name+" at "+angle);
                        }
                }
        }
        private static void Require(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException("Motel layout: "+message);
        }
    }
}
