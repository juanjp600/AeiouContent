using System.Reflection;
using UnityEngine;

namespace AeiouContent;

static class Util
{
    static readonly MethodInfo? GetBodypartMethodInfo = typeof(PlayerRagdoll).GetMethod("GetBodypart", BindingFlags.Instance | BindingFlags.NonPublic);

    public static Bodypart? GetBodypart(Player? player, BodypartType type)
    {
        if (player is null) { return null; }
        if (GetBodypartMethodInfo is null) { return null; }
        return GetBodypartMethodInfo.Invoke(player.refs.ragdoll, [type]) as Bodypart;
    }

    public static void GetTtsGameObjects(Player? player, out GameObject? sourceObject, out GameObject? ttsObject)
    {
        var sourceMonoBehavior = player != Player.localPlayer && player is { data.dead: false }
            ? Util.GetBodypart(player, BodypartType.Head)
            : (MonoBehaviour?)MainCamera.instance;
        sourceObject = sourceMonoBehavior != null ? sourceMonoBehavior.gameObject : null;
        ttsObject = null;

        if (sourceObject == null) { return; }

        var prevTtsGameObject = sourceObject.transform.Find(Plugin.TtsGameObjectName);
        ttsObject = prevTtsGameObject != null ? prevTtsGameObject.gameObject : null;
    }

    public static void UpdateMicrophoneValue(Player player)
    {
        if (player.data.dead) { return; }

        GetTtsGameObjects(player, out _, out var ttsObject);
        if (ttsObject == null) { return; }

        var aeiouTimeTracker = ttsObject.GetComponent<AeiouTimeTracker>();
        if (aeiouTimeTracker == null) { return; }
        if (Time.unscaledTime > aeiouTimeTracker.speakStopTime) { return; }

        player.data.microphoneValue = aeiouTimeTracker.loudness;
    }

    public static Vector3 GetVerticallyScaledPosition(Vector3 position)
        => new(x: position.x, y: position.y / Plugin.TextVisibilityVerticalRangeScaling, z: position.z);
}