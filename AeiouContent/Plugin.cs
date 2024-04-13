using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace AeiouContent;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public sealed class Plugin : BaseUnityPlugin
{
    internal const string Guid = "AeiouContent";
    internal const string Name = "AeiouContent";
    internal const string Version = "0.2.0";

    public const string TtsGameObjectName = "AeiouContentGameObject";
    public const float MaxTtsAudibleDistance = 60f;
    public const float MaxTextVisibleDistance = 45f;
    public const float TextVisibilityVerticalRangeScaling = 0.3f;
    public const float TtsVolumeBoost = 1.2f;

    private Harmony _harmony = null!;
    internal static Plugin Instance = null!;
    internal ManualLogSource SelfLogger => this.Logger;
    internal SpeakProcessOwner SpeakProcessOwner = null!;

    private void Awake()
    {
        Instance = this;

        SpeakProcessOwner = new SpeakProcessOwner();
        SpeakProcessOwner.Init();
        _harmony = new Harmony(Guid);
        _harmony.PatchAll();
        Logger.LogInfo($"{Name} loaded!");
    }
}