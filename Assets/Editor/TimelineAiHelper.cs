using System.Collections.Generic;
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
        
        // Analysiert den Clip und gibt die Zeiten (in Sekunden) der markantesten Beat-Drops zurück.
        public static string GetAudioClipTransients(string audioClipPath, out List<float> beatDropsSeconds)
        {
            beatDropsSeconds = new List<float>();
            if (!TryGetAudioClip(audioClipPath, out var clip))
            {
                return BridgeProtocol.Failure("No AudioClip could be loaded from path: " + audioClipPath);
            }
            // 1. Audiodaten auslesen (float[] funktioniert nativ mit GetData)
            var numSamples = clip.samples * clip.channels;
            float[] samples = new float[numSamples];
            
            if (!clip.GetData(samples, 0))
            {
                return BridgeProtocol.Failure("Could not read audio data from clip.");
            }

            // 2. Konfiguration für die Beat-Erkennung
            int windowSize = 1024 * clip.channels; // Analyse-Fenstergröße (~0.02 Sekunden)
            float minTimeBetweenBeats = 0.2f;      // Cooldown in Sekunden (verhindert hunderte Hits beim selben Drop)
            float thresholdMultiplier = 2.5f;      // WIE STARK muss der Drop sein? (Größer = nur die absolut lautesten)
            float noiseFloor = 0.05f;              // Grundlautstärke, die überschritten werden muss
            
            // Historie für die Ermittlung der Durchschnittslautstärke
            Queue<float> energyHistory = new Queue<float>();
            float energyHistorySum = 0f;
            int historySize = 43; // Historie der letzten ~1 Sekunde

            float lastBeatTime = -minTimeBetweenBeats;

            // 3. Durch das Audio gehen in Blöcken (Windows)
            for (int i = 0; i < samples.Length - windowSize; i += windowSize)
            {
                // Energie des aktuellen Blocks berechnen (RMS/Squared)
                float currentEnergy = 0f;
                for (int j = 0; j < windowSize; j++)
                {
                    // Quadrieren macht Spitzen (Laute Töne) noch deutlicher
                    currentEnergy += samples[i + j] * samples[i + j]; 
                }
                currentEnergy /= windowSize;

                // Durchschnitt der bisherigen Audio-Historie
                float avgMemoryEnergy = energyHistory.Count > 0 ? (energyHistorySum / energyHistory.Count) : 0.01f;

                // 4. BEAT CHECK: Ist die Energie hier VIEL höher als im Durchschnitt?
                if (currentEnergy > (avgMemoryEnergy * thresholdMultiplier) && currentEnergy > noiseFloor)
                {
                    // Index (Sample) in Sekunden umrechnen
                    float timeInSeconds = (float)i / (clip.frequency * clip.channels);

                    // Checken ob der letzte Beat weit genug weg ist (Cooldown)
                    if (timeInSeconds - lastBeatTime >= minTimeBetweenBeats)
                    {
                        beatDropsSeconds.Add(timeInSeconds);
                        lastBeatTime = timeInSeconds;
                    }
                }

                // 5. Historie updaten (Ringpuffer)
                energyHistory.Enqueue(currentEnergy);
                energyHistorySum += currentEnergy;
                if (energyHistory.Count > historySize)
                {
                    energyHistorySum -= energyHistory.Dequeue();
                }
            }

            return BridgeProtocol.SUCCESS;
        }
        
        static bool TryGetAudioClip(string assetPath, out AudioClip audioClip)
        {
            audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            return audioClip != null;
        }
    }
}