using UnityEngine;
using Verse;

namespace PawnStorages;

[StaticConstructorOnStartup]
public static class PSContent
{
    public static Texture2D DirectionArrow = ContentFinder<Texture2D>.Get("UI/Direction");
    public static readonly Texture2D PS_ArrowUp = ContentFinder<Texture2D>.Get("UI/Buttons/PS_ArrowUp");
    public static readonly Texture2D PS_ArrowDown = ContentFinder<Texture2D>.Get("UI/Buttons/PS_ArrowDown");
}
