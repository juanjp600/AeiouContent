using System.Linq;
using CWChat;
using ExitGames.Client.Photon;
using HarmonyLib;
using UnityEngine;

namespace AeiouContent;

[HarmonyPatch(declaringType: typeof(ChatNet))]
sealed class SimpleChatHook
{
    private const byte ChatMessageCode = 16;

    [HarmonyPatch(methodName: nameof(ChatNet.OnEvent))]
    [HarmonyPrefix]
    private static bool OnEventPrefix(ref ChatNet __instance, EventData photonEvent)
    {
        if (photonEvent.Code != ChatMessageCode) { return false; }

        if (photonEvent.CustomData is not (object[] and [string name, string message, bool isDead, string faceColor, ..])) { return false; }
        var msgData = new MsgData(name, message, isDead, faceColor);
        var actorNumber = photonEvent.Sender;
        Plugin.Instance.SelfLogger.LogMessage($"Message from {msgData.name}: {msgData.message}");

        var matchingPlayer = PlayerHandler.instance.players.FirstOrDefault(p => p.refs.view.Owner.ActorNumber == actorNumber);
        if (matchingPlayer is null) { return false; }
        if (!Player.localPlayer.data.dead && matchingPlayer.data.dead) { return false; }

        msgData.name = $"{matchingPlayer.refs.view.Controller.NickName} ( {matchingPlayer.refs.visor.visorFaceText.text} )";

        var posMatching = Util.GetBodypart(matchingPlayer, BodypartType.Head)?.transform.position ?? Vector3.zero;
        var posSelf = Util.GetBodypart(Player.localPlayer, BodypartType.Head)?.transform.position ?? Vector3.zero;

        var rawDistance = Vector3.Distance(
            posMatching,
            posSelf);

        Plugin.Instance.SelfLogger.LogDebug($"Distance from {matchingPlayer.refs.view.Controller.NickName}: {rawDistance}");
        if (rawDistance <= Plugin.MaxTtsAudibleDistance)
        {
            Plugin.Instance.SpeakProcessOwner.Speak(msgData.message, matchingPlayer);
        }

        var verticallyScaledDistance = Vector3.Distance(
            Util.GetVerticallyScaledPosition(posMatching),
            Util.GetVerticallyScaledPosition(posSelf));
        if (verticallyScaledDistance <= Plugin.MaxTextVisibleDistance)
        {
            __instance.AddNewMessage(msgData);
        }

        return false;
    }
}
