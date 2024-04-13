using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace AeiouContent;

[HarmonyPatch(declaringType: typeof(PlayerSyncer))]
sealed class PlayerSyncerHook
{
    [HarmonyPatch(methodName: "OnPhotonSerializeView")]
    [HarmonyPostfix]
    private static void OnPhotonSerializeViewPostfix(ref PlayerSyncer __instance, PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) { return; }

        var playerField = typeof(PlayerSyncer).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);
        var player = playerField?.GetValue(__instance) as Player;
        if (player == null || player.data == null) { return; }

        Util.UpdateMicrophoneValue(player);
    }
}