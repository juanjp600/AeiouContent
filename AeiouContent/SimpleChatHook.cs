using System.Linq;
using CWChat;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace AeiouContent;

[HarmonyPatch(declaringType: typeof(ChatNet))]
sealed class SimpleChatHook
{
    private const byte ChatMessageCode = 16;
    
    [HarmonyPatch(methodName: nameof(ChatNet.SendMessage))]
    [HarmonyPrefix]
    private static bool SendMessagePrefix(ref ChatNet __instance, MsgData msg)
    {
        var player = Player.localPlayer;
        var val = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };
        PhotonNetwork.RaiseEvent(
            eventCode: ChatMessageCode,
            eventContent: new object[]
            {
                $"{PhotonNetwork.LocalPlayer.NickName} ( {Player.localPlayer.refs.visor.visorFaceText.text} )",
                msg.message,
                msg.dead,
                msg.hex,
                PhotonNetwork.LocalPlayer.ActorNumber
            },
            raiseEventOptions: val,
            sendOptions: SendOptions.SendReliable);
        return false;
    }

    [HarmonyPatch(methodName: nameof(ChatNet.OnEvent))]
    [HarmonyPrefix]
    private static bool OnEventPrefix(ref ChatNet __instance, EventData photonEvent)
    {
        if (photonEvent.Code != ChatMessageCode) { return false; }

        var array = (object[])photonEvent.CustomData;
        if (array is not { Length: >= 5 }) { return false; }
        var msg = new MsgData(array[0].ToString(), array[1].ToString(), (bool)array[2], array[3].ToString());
        if (array[4] is not int actorNumber) { return false; }
        Plugin.Instance.SelfLogger.LogMessage($"Message from {msg.name}: {msg.message}");

        var matchingPlayer = PlayerHandler.instance.players.FirstOrDefault(p => p.refs.view.Owner.ActorNumber == actorNumber);
        if (matchingPlayer is null)
        {
            return false;
        }
        if (!Player.localPlayer.data.dead && matchingPlayer.data.dead) { return false; }

        var posMatching = Util.GetBodypart(matchingPlayer, BodypartType.Head)?.transform.position ?? Vector3.zero;
        var posSelf = Util.GetBodypart(Player.localPlayer, BodypartType.Head)?.transform.position ?? Vector3.zero;
        var distance = Vector3.Distance(posMatching, posSelf);

        Plugin.Instance.SelfLogger.LogInfo($"Distance from {matchingPlayer.refs.view.Controller.NickName}: {distance}");
        if (distance > Plugin.MaxTtsAudibleDistance) { return false; }
        Speak(msg, matchingPlayer);

        if (distance > Plugin.MaxTextVisibleDistance) { return false; }
        __instance.AddNewMessage(msg);


        return false;
    }

    private static void Speak(MsgData msg, Player player)
    {
        Plugin.Instance.SpeakProcessOwner.Speak(msg.message, player);
    }
}
