using System;
using System.Linq;
using HarmonyLib;
//using SteamAudio;
using UnityEngine;
using Zorro.Core;
using Vector3 = UnityEngine.Vector3;

namespace AeiouContent;

[HarmonyPatch(declaringType: typeof(RagdollHandler))]
static class RagdollHandlerHook
{
    private static readonly GameObject?[] AudioSourceGameObjects = new GameObject[NumSources];
    private static int _lastUsedAudioSource = -1;

    private const int NumSources = 8;
    private const int MaxSeconds = 40;
    private const int Frequency = 11025;
    private const int MaxSamples = MaxSeconds * Frequency;
    private static readonly float[] UploadBuffer = new float[MaxSamples];

    private static GameObject GetAudioSourceGameObject()
    {
        _lastUsedAudioSource = (_lastUsedAudioSource + 1) % AudioSourceGameObjects.Length;
        var audioSourceGameObject = AudioSourceGameObjects[_lastUsedAudioSource];
        if (audioSourceGameObject != null) { return audioSourceGameObject; }

        audioSourceGameObject = new GameObject(Plugin.TtsGameObjectName);
        var audioSource = audioSourceGameObject.AddComponent<AudioSource>();
        //audioSourceGameObject.AddComponent<SteamAudioSource>();
        audioSourceGameObject.AddComponent<AeiouTimeTracker>();
        AudioSourceGameObjects[_lastUsedAudioSource] = audioSourceGameObject;
        audioSource.clip ??= AudioClip.Create(
            name: "AeiouClip",
            lengthSamples: MaxSamples,
            channels: 1,
            frequency: Frequency,
            stream: false);
        return audioSourceGameObject;
    }

    [HarmonyPatch(methodName: "FixedUpdate")]
    [HarmonyPostfix]
    private static void FixedUpdatePostfix(ref RagdollHandler __instance)
    {
        while (Plugin.Instance.SpeakProcessOwner.IncomingAudioData.TryDequeue(out var audio))
        {
            GameObject? ttsGameObject = null;
            Util.GetTtsGameObjects(audio.Player, out var sourceObject, out var prevTtsGameObject);
            if (prevTtsGameObject != null)
            {
                prevTtsGameObject.transform.parent = null;
                var prevAudioSource = prevTtsGameObject.GetComponent<AudioSource>();
                if (prevAudioSource != null) { prevAudioSource.Stop(); }
                ttsGameObject = prevTtsGameObject;
            }
            else if (ttsGameObject == null)
            {
                ttsGameObject = GetAudioSourceGameObject();
            }
            Array.Clear(array: UploadBuffer, index: 0, length: UploadBuffer.Length);
            var audioSource = ttsGameObject.GetComponent<AudioSource>();
            //var steamAudioSource = ttsGameObject.GetComponent<SteamAudioSource>();
            var aeiouTimeTracker = ttsGameObject.GetComponent<AeiouTimeTracker>();
            audioSource.Stop();
            audioSource
                .clip
                .SetData(data: UploadBuffer, offsetSamples: 0);
            for (int i = 0; i < Math.Min(audio.Data.Length / 2, UploadBuffer.Length); i++)
            {
                ushort sample = (ushort)(audio.Data[i * 2] | (audio.Data[(i * 2) + 1] << 8));
                UploadBuffer[i] = Math.Clamp(
                    value: ((float)unchecked((short)sample) / (float)short.MaxValue) * Plugin.TtsVolumeBoost,
                    min: -1f,
                    max: 1f);
            }

            var lengthSeconds = (float)Math.Min(audio.Data.Length / 2, UploadBuffer.Length) / (float)Frequency;
            aeiouTimeTracker.speakStopTime = Time.unscaledTime + lengthSeconds;
            aeiouTimeTracker.loudness = Mathf.Lerp(
                a: 0.4f,
                b: 1.0f,
                t: Math.Min(1.0f, audio.MessageText.Count(c => c == '!' || char.IsUpper(c)) * 0.15f));
            audioSource
                .clip
                .SetData(data: UploadBuffer, offsetSamples: 0);
            audioSource.outputAudioMixerGroup = SingletonAsset<MixerHolder>.Instance.voiceMixer.audioMixer.outputAudioMixerGroup;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 10f;
            audioSource.maxDistance = Plugin.MaxTtsAudibleDistance;
            audioSource.spatialize = audio.Player != Player.localPlayer && audio.Player is { data.dead: false };
            audioSource.spatialBlend = audio.Player != Player.localPlayer && audio.Player is { data.dead: false } ? 1f : 0f;
            audioSource.dopplerLevel = 0f;
            /*steamAudioSource.occlusion = audio.Player is { data.dead: false } || Player.localPlayer is { data.dead: false };
            steamAudioSource.reflections = audio.Player is { data.dead: false };
            steamAudioSource.transmission = audio.Player is { data.dead: false };
            steamAudioSource.distanceAttenuation = audio.Player is { data.dead: false };*/
            ttsGameObject.transform.parent = null;
#if TEST_CAMERA_AS_SOURCE
            if (audio.Player == Player.localPlayer)
            {
                var videoCamera = UnityEngine.Object.FindObjectOfType<VideoCamera>();
                sourceObject = videoCamera != null && videoCamera.gameObject != null ? videoCamera.gameObject : sourceObject;
            }
#endif
            ttsGameObject.transform.position = sourceObject != null ? sourceObject.transform.position : Vector3.zero;
            ttsGameObject.transform.parent = sourceObject != null ? sourceObject.transform : null;
            audioSource.Play();
        }
    }
}