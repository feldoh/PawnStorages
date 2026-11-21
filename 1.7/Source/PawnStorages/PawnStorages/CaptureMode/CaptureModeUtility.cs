using System.Linq;
using Verse;

namespace PawnStorages.CaptureMode;

public static class CaptureModeUtility
{
    public static CaptureModeDef DefaultMode => DefDatabase<CaptureModeDef>.GetNamed("PS_Capture_Mode_Enslave", false) ?? DefDatabase<CaptureModeDef>.AllDefs.FirstOrDefault();
}
