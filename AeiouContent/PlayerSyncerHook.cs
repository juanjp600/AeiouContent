using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace AeiouContent;

[HarmonyPatch(declaringType: typeof(PlayerSyncer))]
sealed class PlayerSyncerHook
{
    private static readonly FieldInfo? PlayerField =
        typeof(PlayerSyncer).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);

    [HarmonyPatch(methodName: "OnPhotonSerializeView")]
    [HarmonyPostfix]
    private static void OnPhotonSerializeViewPostfix(ref PlayerSyncer __instance, PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) { return; }

        var player = PlayerField?.GetValue(__instance) as Player;
        if (player == null || player.data == null) { return; }

        Util.UpdateMicrophoneValue(player);
    }
}