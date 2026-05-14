using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    // TODO: Add 
    public static class TimelineAiHelper
    {
        /// <returns>between the target and the origin is a clear line of sight</returns>
        public static bool HasLineOfSightToTarget(Vector3 origin, Vector3 target)
        {
            var toTarget = origin - target;
            var distance = toTarget.magnitude;
            // TODO: Convert to Spherecast, but objects that are in start/endsphere wont be captured
            // TODO: Maybe pass target and origin "object" to exclude them from the ray
            return !Physics.Raycast(origin, toTarget.normalized, distance);
        }
        
        /// <returns>The distance to the forward collision or null if nothing hit</returns>
        public static float? DistanceToForwardCollision(Vector3 origin, Vector3 forward)
        {
            if (!Physics.Raycast(origin, forward, out var hit, 100f))
            {
                return null;
            }

            return hit.distance;
        }
        ///<summary>Analyzes the beat and returns times (in seconds) with intensity</summary>
        ///<returns> Item1 = timestamp in seconds, Item2 = normalized intensity 0–1 relative to strongest beat.</returns>
        public static string GetAudioClipTransients(string audioClipPath, out List<(float time, float intensity)> beatDrops)
        {
            beatDrops = new List<(float, float)>();
            if (!TryGetAudioClip(audioClipPath, out var clip))
            {
                return BridgeProtocol.Failure("No AudioClip could be loaded from path: " + audioClipPath);
            }
            // read audiodata
            var numSamples = clip.samples * clip.channels;
            float[] samples = new float[numSamples];

            if (!clip.GetData(samples, 0))
            {
                return BridgeProtocol.Failure("Could not read audio data from clip.");
            }

            // beat recognition config
            int windowSize = 1024 * clip.channels;
            float minTimeBetweenBeats = 0.2f;
            float thresholdMultiplier = 2.5f;
            float noiseFloor = 0.05f;

            Queue<float> energyHistory = new Queue<float>();
            float energyHistorySum = 0f;
            int historySize = 43;

            float lastBeatTime = -minTimeBetweenBeats;
            var rawBeats = new List<(float time, float energy)>();

            // 3. go throgh audio
            for (int i = 0; i < samples.Length - windowSize; i += windowSize)
            {
                float currentEnergy = 0f;
                for (int j = 0; j < windowSize; j++)
                    currentEnergy += samples[i + j] * samples[i + j];
                currentEnergy /= windowSize;

                float avgMemoryEnergy = energyHistory.Count > 0 ? (energyHistorySum / energyHistory.Count) : 0.01f;

                if (currentEnergy > (avgMemoryEnergy * thresholdMultiplier) && currentEnergy > noiseFloor)
                {
                    float timeInSeconds = (float)i / (clip.frequency * clip.channels);

                    if (timeInSeconds - lastBeatTime >= minTimeBetweenBeats)
                    {
                        rawBeats.Add((timeInSeconds, currentEnergy));
                        lastBeatTime = timeInSeconds;
                    }
                }

                energyHistory.Enqueue(currentEnergy);
                energyHistorySum += currentEnergy;
                if (energyHistory.Count > historySize)
                    energyHistorySum -= energyHistory.Dequeue();
            }

            // normalize intensity
            if (rawBeats.Count == 0)
                return BridgeProtocol.SUCCESS;

            float maxEnergy = rawBeats.Max(b => b.energy);
            foreach (var (time, energy) in rawBeats)
                beatDrops.Add((time, energy / maxEnergy));

            return BridgeProtocol.SUCCESS;
        }
        
        static bool TryGetAudioClip(string assetPath, out AudioClip audioClip)
        {
            audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            return audioClip != null;
        }
    }
}