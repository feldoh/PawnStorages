using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace PawnStorages.Anomaly;

/// <summary>
/// Quest script for the "All" extraction encounter.
/// Spawning and state management is handled by AllFeature_GameComponent.
/// This quest node simply creates a quest log entry for the player.
/// </summary>
public class QuestNode_Root_AllExtraction : QuestNode
{
    public override void RunInt()
    {
        Quest quest = QuestGen.quest;
        Slate slate = QuestGen.slate;

        Pawn allPawn = slate.Get<Pawn>("allPawn");
        Pawn nothingPawn = slate.Get<Pawn>("nothingPawn");

        if (allPawn != null)
        {
            string questTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("PS_AllExtraction");
            QuestUtility.AddQuestTag(ref allPawn.questTags, questTag);
            if (nothingPawn != null)
                QuestUtility.AddQuestTag(ref nothingPawn.questTags, questTag);
        }

        quest.description = "PS_Anomaly_QuestDescription".Translate();
    }

    public override bool TestRunInt(Slate slate)
    {
        return slate.Get<Map>("map") != null;
    }
}
