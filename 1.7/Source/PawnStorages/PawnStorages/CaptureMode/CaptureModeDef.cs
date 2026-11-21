using System;
using Verse;

namespace PawnStorages.CaptureMode;

public class CaptureModeDef : Def
{
    public Type modeClass;

    private CaptureMode mode;

    public CaptureMode Mode
    {
        get
        {
            if (mode == null && modeClass != null)
            {
                mode = Activator.CreateInstance(modeClass) as CaptureMode;
                if (mode != null)
                    mode.Def = this;
            }
            return mode;
        }
    }
}
