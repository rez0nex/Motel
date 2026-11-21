using UnityEngine;
namespace Motel.Editor
{
    public static class MotelBypassSceneSetup
    {
        public static Vector3 Position => MotelTechnicalRoomSceneSetup.World(3.25f,2.15f,6.60f);
        public static Vector3 Approach => MotelTechnicalRoomSceneSetup.Approach;
        public static void Create(Transform root) => MotelTechnicalRoomSceneSetup.Create(root);
    }
}