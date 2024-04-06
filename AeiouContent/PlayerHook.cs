using HarmonyLib;

namespace AeiouContent;

[HarmonyPatch(declaringType: typeof(Player))]
sealed class PlayerHook
{
    [HarmonyPatch(methodName: "OnGetMic")]
    [HarmonyPostfix]
    private static void OnGetMicPostfix(ref Player __instance)
    {
        Util.UpdateMicrophoneValue(__instance);
    }
}