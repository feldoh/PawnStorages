using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages
{
    public class CompProperties_PSAssignableToPawn: CompProperties_AssignableToPawn
    {
        public bool colonyAnimalsOnly = false;
        public bool toolUsersOnly = false;
        public bool disallowEntityStoringCommand = false;
        public bool showGizmo = true;
        public bool drawAsFrozenInCarbonite = false;
        public bool drawGrayscale = true;
        public float pawnDrawScaleX = 1f;
        public float pawnDrawScaleZ = 1f;
        public float pawnDrawOffsetX = 0f;
        public float pawnDrawOffsetZ = 0f;
        public float cameraZoom = 1.5f;
        public CompProperties_PSAssignableToPawn() => compClass = typeof(CompAssignableToPawn_PawnStorage);

    }
}
