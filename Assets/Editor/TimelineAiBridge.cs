using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEditor;

// TODO:
// Verbote sollten IMMER von den Methoden mitgeprüft werden, beispiel: was passiert wenn wir ein cinemachine asset überschreiben? DAS MUSS GEHANDLED WERDEN!
// -> Das wird zwar leicht behandelt aber bei track überschreiben muss er auch geleert werden!!
// Prüfen ob es schon einen playable director gibt (done)
// prüfen ob es schon einen cinemachine Track mit dem gleichen Namen gibt?


// Assets are saved and returned as strings
// Scene GameObjects are returned as their Type
public static class TimelineAiBridge
{
    const string FolderPath = "Assets";
    const string Format = "playable";


    /// <param name="name">name of the timeline asset. This will be relevant to all other method calls, since they need to query the asset</param>
    /// <param name="timelineAssetName">the name of the created asset</param>
    public static string TryCreateTimelineAsset(string name, out string timelineAssetName)
    {
        timelineAssetName = string.Empty;
        var skipValidation = name.Contains("_OVERRIDE");
        if (skipValidation)
        {
            name = name.Replace("_OVERRIDE", "");
        }

        if (GetTimelineAsset(name, out _, out var fullPath) && !skipValidation)
        {
            // already existing timeline-asset found (same name)
            return BridgeProtocol.AwaitingInput("A Timeline asset with the name '" + name + "' already exists. Override it?",
                "Call TryCreateAsset again with the name + '_OVERRIDE'",
                "Stop and do nothing");
        }

        timelineAssetName = name;
        return CreateTimelineAsset(fullPath);
    }
    static string CreateTimelineAsset(string fullPath)
    {
        var timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timelineAsset, fullPath);
        AssetDatabase.SaveAssets();
        return BridgeProtocol.SUCCESS;
    }
    static bool GetTimelineAsset(string name, out TimelineAsset timelineAsset, out string fullPath)
    {
        fullPath = $"{FolderPath}/{name}.{Format}";
        timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(fullPath);
        return timelineAsset != null;
    }

    static bool TryParseGid(string gidString, out GlobalObjectId gid)
    {
        if (string.IsNullOrEmpty(gidString))
        {
            gid = default;
            return false;
        }
        string cleanGid = gidString.Trim('[', ']', ' ');
        return GlobalObjectId.TryParse(cleanGid, out gid);
    }

    static bool TryGetTrackAsset<T>(TimelineAsset timeline, string trackAssetName, out T trackAsset) where T : TrackAsset, new()
    {
        trackAsset = timeline.GetOutputTracks()
            .OfType<T>()
            .FirstOrDefault(t => t.name == trackAssetName);

        return trackAsset != null;
    }

    /// <summary>
    /// Creates an Animation clip with a certain duration and references it to a new TimelineClip, which is then added to an existing Animation track
    /// </summary>
    /// <param name="timelineName">Name of the timeline asset</param>
    /// <param name="animationTrackName">the name of the already existing animation track that we want to add a new animation to</param>
    /// <param name="animationTimelineClipName">name of the animation timeline clip that we just created in the track. Is later used to identify the exact timeline clip to add the animation per curve to</param>
    /// <returns>the name of the created animation track</returns>
    public static string AddAnimationToAnimationTrack(string timelineName, string animationTrackName, string animationName, float startTime, float duration, out string animationTimelineClipName)
    {
        animationTimelineClipName = string.Empty;
        if (duration < 0)
        {
            return BridgeProtocol.Failure("duration must be >= 0.");
        }
        if (startTime < 0)
        {
            return BridgeProtocol.Failure("startTime must be >= 0.");
        }
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return BridgeProtocol.Failure("No Timeline-Asset found under this path: " + timelineName);
        }
        if (!TryGetTrackAsset<AnimationTrack>(timeline, animationTrackName, out var animationTrack))
        {
            return BridgeProtocol.Failure("No AnimationTrack named '" + animationTrackName + "' found on the timeline.");
        }
        
        AnimationClip clip = new AnimationClip();
        // TODO: Could give the clip a name to be idenfied in the assetdatabase.
        
        // 2 create clip
        AssetDatabase.AddObjectToAsset(clip, timeline);
        var animClip = animationTrack.CreateClip(clip);
        animClip.start = startTime;
        animClip.duration = duration;
        animClip.displayName = animationName;
        animationTimelineClipName = animationName; // set to the same name that was put in to remind the agent: hey this is what you passed in

        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return BridgeProtocol.SUCCESS;
    }
    
    /// <param name="timelineName">Name of the timeline asset</param>
    /// <param name="animatedObjGlobalId">The global id of the gameobject that is to be animated. The Track will have a reference to that gameobjects animator that will be created in this method</param>
    /// <param name="directorGlobalId">global id of the director component</param>
    /// <param name="animationTrackName">will return the trackname that was created via out-keyword</param>
    public static string TryCreateAnimationTrack(string timelineName, string animatedObjGlobalId, string directorGlobalId, out string animationTrackName)
    {
        animationTrackName = string.Empty;
        
        if (!TryParseGid(animatedObjGlobalId, out var animatedObjGid))
        {
            return BridgeProtocol.Failure("The Global Object ID for the animated object you provided is not parsable: " + animatedObjGlobalId);
        }

        var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(animatedObjGid);
        Transform animatedTransform = null;
        if (obj is GameObject go)
        {
            animatedTransform = go.transform;
        }
        else if (obj is Component comp)
        {
            animatedTransform = comp.transform;
        }

        if (animatedTransform == null)
        {
            return BridgeProtocol.Failure("Could not resolve a Transform from the provided animatedObjGlobalId.");
        }

        // pre track setup, the animated transform needs an animator to work with the track
        var animator = animatedTransform.GetComponent<Animator>();
        if (animator == null)
        {
            animator = animatedTransform.gameObject.AddComponent<Animator>();
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
        animationTrackName = animatedTransform.name;

        return TryCreateTrack<AnimationTrack>(timelineName, directorGlobalId, animationTrackName, animator);
    }

    // TODO: Query ALL tracks because we allow ONLY 1 Cinemachine Track, Cancel out ERROR
    /// <summary>
    /// 
    /// </summary>
    /// <param name="timelineName">Name of the timeline asset</param>
    /// <param name="cinemachineBrainGlobalId">The global id of the cinemachine-brain-component that the cinemachine track will refer to</param>
    /// <param name="directorGlobalId">global id of the director component</param>
    /// <param name="cinemachineTrackName">will return the trackname that was created via out-keyword</param>
    public static string TryCreateCinemachineTrack(string timelineName, string directorGlobalId,
        string cinemachineBrainGlobalId, out string cinemachineTrackName)
    {
        cinemachineTrackName = string.Empty;
        if (!TryParseGid(cinemachineBrainGlobalId, out var brainGid))
        {
            return BridgeProtocol.Failure("The Global Object ID for the brain you provided is not parsable: " +
                                          cinemachineBrainGlobalId);
        }

        var brain = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(brainGid) as CinemachineBrain;
        if (brain == null)
        {
            return BridgeProtocol.Failure("Could not resolve a CinemachineBrain from the provided brainGlobalId.");
        }

        // Theres only one cinemachine track allowed
        cinemachineTrackName = "Cinemachine Track";
        return TryCreateTrack<CinemachineTrack>(timelineName, directorGlobalId, cinemachineTrackName, brain);
    }


    // TODO: Tracks have the out Param of the name of the Track

    static string TryCreateTrack<T>(string timelineName, string directorGlobalId, string newTrackName, [CanBeNull] UnityEngine.Object trackReference) where T : TrackAsset
        // we tell the compiler that any trackasset can be created via a public parameterless constructor
        , new()
    {
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return BridgeProtocol.Failure("No Timeline-Asset found under this path: " + timelineName);
        }
        if (!TryParseGid(directorGlobalId, out var directorGid))
        {
            return BridgeProtocol.Failure("The Global Object ID for the director you provided is not parsable: " + directorGlobalId);
        }
        var director = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(directorGid) as PlayableDirector;
        if (director == null)
        {
            return BridgeProtocol.Failure("Could not resolve a PlayableDirector from the provided directorGlobalId.");
        }
        
        CreateTrack<T>(timeline, director, newTrackName, trackReference);
        return BridgeProtocol.SUCCESS;
    }

    static void CreateTrack<T>(TimelineAsset timeline, PlayableDirector director, string newTrackName, [CanBeNull] UnityEngine.Object trackReference) where T : TrackAsset, new()
    {
        var track = timeline.CreateTrack<T>(null, newTrackName);
        
        // the created track needs a scene reference -> thats what we set here:
        if (trackReference != null)
        {
            director.SetGenericBinding(track, trackReference);
        }
        // object change in scene only needs to be marked dirty, can be saved manually from user.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        // asset needs to be marked dirty and saved
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
    }

    /// <param name="directorName">Name that the director should have, "_Director" will be appended</param>
    /// <param name="timelineName"></param>
    /// <param name="directorGlobalId"></param>
    /// <returns>The name of the created playable director in the scene</returns>
    public static string CreatePlayableDirector(string directorName, string timelineName, out string directorGlobalId)
    {
        directorGlobalId = string.Empty;
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return BridgeProtocol.Failure("No Timeline-Asset found under this path: " + timelineName);
        }

        var directorGo = new GameObject(directorName + "_Director");
        var director = directorGo.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        
        directorGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(director).ToString();
        
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        return BridgeProtocol.SUCCESS;
    }

    /// <param name="timelineName">name of the timeline asset</param>
    /// <param name="trackName">name of the animation track where we want to add an animation to</param>
    /// <param name="timelineClipName">The name of the timeline clip that is inside the animation track where we want to add our animation to</param>
    /// <param name="propertyName">Name of the property that we are trying to access via reflection like "Lens.dutch" from the generic type</param>
    /// <param name="trackPositions">
    ///     List of (splinePosition, clipTime) tuples.
    ///     splinePosition: 0-1 value representing where on the spline.
    ///     clipTime: time in seconds within the clip.
    /// </param>
    static string SetAnimationTrackCurve<T>(string timelineName, string trackName, string timelineClipName, 
        string propertyName, List<(float value, float clipTime)> trackPositions) 
    {
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return BridgeProtocol.Failure("No Timeline-Asset found under this path: " + timelineName);
        }

        var track = timeline.GetOutputTracks()
            .OfType<AnimationTrack>()
            .FirstOrDefault(t => t.name == trackName);

        if (track == null)
        {
            return BridgeProtocol.Failure("No AnimationTrack named '" + trackName + "' found on the timeline.");
        }

        if (trackPositions.Any(e => e.clipTime < 0))
        {
            return BridgeProtocol.Failure("All clipTime values must be >= 0.");
        }

        // Dolly Curve
        AnimationCurve curve = new();
        foreach (var trackPosition in trackPositions)
        {
            curve.AddKey(trackPosition.clipTime, trackPosition.value);
        }

        var clip = track.GetClips()
            .FirstOrDefault(e => e.displayName == timelineClipName);

        if (clip == null)
        {
            return BridgeProtocol.Failure("No clip named '" + timelineClipName + "' found on track '" + trackName + "'.");
        }

        var animClip = clip.animationClip;
        // "" means no parent
        if (animClip == null)
        {
            return BridgeProtocol.Failure("The AnimationClip is null.");
        }
        
        animClip.SetCurve("", typeof(T), propertyName, curve);

        EditorUtility.SetDirty(animClip);
        EditorUtility.SetDirty(timeline); // because animclip is subasset of timeline
        AssetDatabase.SaveAssets();

        return BridgeProtocol.SUCCESS;
    }

    // TODO: Method can be simplified, since first 3/4 is repetitive

    /// <param name="timelineName">exact name of the timeline</param>
    /// <param name="trackName">name of the cinemachine track</param>
    /// <param name="assignedCameraGlobalId">the virtual camera that is to be active in the track at the given time</param>
    /// <param name="directorGlobalId">global id of the director component</param>
    public static string SetCinemachineTrack(string timelineName, string trackName, string assignedCameraGlobalId, string directorGlobalId, float startTime, float duration)
    {
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return BridgeProtocol.Failure("No Timeline-Asset found under this path: " + timelineName);
        }

        if (!TryParseGid(directorGlobalId, out var directorGid))
        {
            return BridgeProtocol.Failure("The Global Object ID for the director you provided is not parsable: " + directorGlobalId);
        }
        var director = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(directorGid) as PlayableDirector;
        if (director == null)
        {
            return BridgeProtocol.Failure("Could not resolve a PlayableDirector from the provided directorGlobalId.");
        }

        if (!TryParseGid(assignedCameraGlobalId, out var cameraGid))
        {
            return BridgeProtocol.Failure("The Global Object ID for the assigned camera you provided is not parsable: " + assignedCameraGlobalId);
        }
        var assignedCamera = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(cameraGid) as CinemachineCamera;
        if (assignedCamera == null)
        {
            return BridgeProtocol.Failure("Could not resolve a CinemachineCamera from the provided assignedCameraGlobalId.");
        }

        if (!TryGetTrackAsset<CinemachineTrack>(timeline, trackName, out var cmTrack))
        {
            return BridgeProtocol.Failure("No CinemachineTrack named '" + trackName + "' found on the timeline.");
        }

        if (duration < 0)
        {
            return BridgeProtocol.Failure("duration must be >= 0.");
        }
        if (startTime < 0)
        {
            return BridgeProtocol.Failure("startTime must be >= 0.");
        }

        var shotClip = cmTrack.CreateClip<CinemachineShot>();
        shotClip.start = startTime;
        shotClip.duration = duration;

        var shot = shotClip.asset as CinemachineShot;
        shot!.VirtualCamera.exposedName = new PropertyName(GUID.Generate().ToString());
        director.SetReferenceValue(shot.VirtualCamera.exposedName, assignedCamera);

        EditorUtility.SetDirty(timeline);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();

        return BridgeProtocol.SUCCESS;
    }

    /// <param name="timelineName">exact name of the timeline</param>
    public static string SetAnimationTrackLensDutch(string timelineName, string trackName, string animationClipName, List<(float dutchValue, float clipTime)> trackPositions)
    {
        return SetAnimationTrackCurve<CinemachineCamera>(timelineName, trackName, animationClipName, "Lens.Dutch",
            trackPositions);
    }

    /// <param name="timelineName">exact name of the timeline</param>
    public static string SetAnimationTrackSplinePosition(string timelineName, string trackName, string animationClipName, List<(float splinePosition, float clipTime)> trackPositions)
    {
        return SetAnimationTrackCurve<CinemachineSplineDolly>(timelineName, trackName, animationClipName, "m_SplineSettings.Position",
            trackPositions);
    }

    /// <param name="timelineName">the name of the timeline asset</param>
    /// <param name="directorGidString">global id of the director component</param>
    /// <param name="audioSourceObjectGid">Leave null if 2D sound. Else: The Gid of the Object where we want the 3D sound to originate.</param>
    /// <param name="audioTrackName">returns the name of the audio track that was created via out-keyword. You need that name later on for adding audio clips to that track</param>
    public static string TryCreateAudioTrack(string timelineName, string directorGidString, [CanBeNull] string audioSourceObjectGid, out string audioTrackName)
    {
        audioTrackName = string.Empty;
        
        // TODO: Refactor into own method?
        // Handle 3d clip? then our gid will not be null and we want a scene reference source
        AudioSource clipSource = null;
        if (audioSourceObjectGid != null)
        {
            if (!TryParseGid(audioSourceObjectGid, out var audioSourceHolderGid))
            {
                return BridgeProtocol.Failure("The Global Object ID for the audio source object you provided is not parsable: " + audioSourceObjectGid);
            }
            var audioSourceHolder = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(audioSourceHolderGid) as Transform;
            if (audioSourceHolder == null)
            {
                return BridgeProtocol.Failure("Could not resolve a Transform from the provided audioSourceHolderGid.");
            }

            if (!audioSourceHolder.TryGetComponent(out clipSource))
            {
                clipSource = audioSourceHolder.AddComponent<AudioSource>();
            }

            audioTrackName = clipSource.gameObject.name;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(clipSource.gameObject.scene);
        }
        else
        {
            audioTrackName = "2D_Audiotrack";
        }
        
        // we pass the [CanBeNull] ClipSource
        return TryCreateTrack<AudioTrack>(timelineName, directorGidString, audioTrackName, clipSource);
    }
    
    // TODO: Thats not the name of the clip but the displayname of
    // TODO: We dont need the director, because we dont create a copy of the audioclip but use the original asset
    /// <param name="timelineName">the name of the timeline asset</param>
    /// <param name="audioTrackName">the audio track name that we want to add the audio clip to</param>
    /// <param name="audioClipFullPath">full assetpath of the audioclip we want to add</param>
    /// <param name="audioTimelineClipName">returns the audio timeline clip name that we created, which you could use to access it later on</param>
    /// <returns></returns>
    public static string AddAudioClipToAudioTrack(string timelineName, string audioTrackName,
        string audioClipFullPath, float startTime, float duration, out string audioTimelineClipName)
    {
        audioTimelineClipName = string.Empty;
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return BridgeProtocol.Failure("No Timeline-Asset found under this path: " + timelineName);
        }

        if (!TryGetAudioClip(audioClipFullPath, out var audioClip))
        {
            return BridgeProtocol.Failure("No AudioClip found at path: " + audioClipFullPath);
        }

        if (duration < 0)
        {
            return BridgeProtocol.Failure("duration must be >= 0.");
        }
        if (startTime < 0)
        {
            return BridgeProtocol.Failure("startTime must be >= 0.");
        }
        
        // 2 create clip
        var audioTrack = timeline.GetOutputTracks()
            .OfType<AudioTrack>().FirstOrDefault(e => e.name == audioTrackName);
        if (audioTrack == null)
        {
            return BridgeProtocol.Failure("The Audiotrack-name you provided doesn't belong to any Audiotrack in the timeline.");
        }
        
        var animClip = audioTrack.CreateClip(audioClip);
        animClip.start = startTime;
        animClip.duration = duration;
        audioTimelineClipName = audioClip.name;
        animClip.displayName = audioTimelineClipName;
        

        EditorUtility.SetDirty(audioClip);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        return BridgeProtocol.SUCCESS;
        // TODO: Optional select a timeframe in that clip that we want to add (like second 3-14)
    }

    static bool TryGetAudioClip(string assetPath, out AudioClip audioClip)
    {
        audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        return audioClip != null;
    }
}
