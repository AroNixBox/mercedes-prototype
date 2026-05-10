using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using Unity.Cinemachine;
using UnityEditor;

// TODO: Rename in the md file instructions

// Assets are saved and returned as strings
// Scene GameObjects are returned as their Type
public static class TimelineAiBridge
{
    const string FolderPath = "Assets";
    const string Format = "playable";


    /// <param name="timelineAssetName">the name of the created asset</param>
    public static string TryCreateAsset(string name, out string timelineAssetName)
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
            return "[[PROMPTRETURN]] AWAITING_INPUT: Override it? " +                                                         
                   "OPTION_1: call TryCreateAsset again with the name + '_OVERRIDE'. " +                     
                   "OPTION_2: stop and do nothing.";
        }
        
        timelineAssetName = name;
        return CreateAsset(fullPath);
    }
    static string CreateAsset(string fullPath)
    {
        var timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timelineAsset, fullPath);
        AssetDatabase.SaveAssets();
        return "[[PROMPTRETURN]] SUCCESS";
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

    // TODO: Convert name to id, because there can be two gameobjects with the same name
    /// <returns>The name of the created playable director in the scene</returns>
    public static string CreatePlayableDirector(string vCamGlobalIdString, string timelineName, out string directorGlobalId)
    {
        directorGlobalId = string.Empty;
        if (!TryParseGid(vCamGlobalIdString, out var vCamGid))
        {
            return "[[PROMPTRETURN]] FAILURE: The Global Object ID for the vcam you provided is not parsable: " + vCamGlobalIdString;
        }

        var vCam = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(vCamGid) as CinemachineCamera;
        if (vCam == null)
        {
            return "[[PROMPTRETURN]] FAILURE: The CinemachineCamera from that GID is null or not a CinemachineCamera";
        }
        
        // 2. create playable director
        var directorGO = new GameObject(vCam.name + "_Director");
        var director = directorGO.AddComponent<PlayableDirector>();
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            // TODO: Maybe instruct to recall
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path: " + timelineName;
        }
        director.playableAsset = timeline;
        
        directorGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(director).ToString();
        return "[[PROMPTRETURN]] SUCCESS";
    }

    /// <param name="timelineName">exact name of the timeline</param>
    public static string CreateCinemachineTrack(string timelineName, string brainGlobalId, string directorGlobalId)
    {
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path: " + timelineName;
        }

        if (!TryParseGid(directorGlobalId, out var directorGid))
            return "[[PROMPTRETURN]] FAILURE: The Global Object ID for the director you provided is not parsable: " + directorGlobalId;
        var director = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(directorGid) as PlayableDirector;
        if (director == null)
            return "[[PROMPTRETURN]] FAILURE: The PlayableDirector from that GID is null or not a PlayableDirector";

        if (!TryParseGid(brainGlobalId, out var brainGid))
            return "[[PROMPTRETURN]] FAILURE: The Global Object ID for the brain you provided is not parsable: " + brainGlobalId;
        var brain = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(brainGid) as CinemachineBrain;
        if (brain == null)
            return "[[PROMPTRETURN]] FAILURE: The CinemachineBrain from that GID is null or not a CinemachineBrain";
        
        // TODO: More descriptive name so we can have multiple tracks
        var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine Track");
        // asset needs to be marked dirty and saved
        EditorUtility.SetDirty(timeline);
        
        // the created track needs a scene reference -> thats what we set here:
        director.SetGenericBinding(cmTrack, brain);
        // object change in scene only needs to be marked dirty, can be saved manually from user.
        EditorUtility.SetDirty(director); // if we dont do this, there is no change that will be saved, not even when the user saves manually
        AssetDatabase.SaveAssets();

        return "[[PROMPTRETURN]] SUCCESS";
    }

    /// <param name="timelineName">exact name of the timeline</param>
    public static string SetCinemachineTrack(string timelineName, string trackName, string assignedCameraGlobalId, string directorGlobalId, float startTime, float duration)
    {
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }

        if (!TryParseGid(directorGlobalId, out var directorGid))
            return "[[PROMPTRETURN]] FAILURE: The Global Object ID for the director you provided is not parsable";
        var director = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(directorGid) as PlayableDirector;
        if (director == null)
            return "[[PROMPTRETURN]] FAILURE: The PlayableDirector from that GID is null";

        if (!TryParseGid(assignedCameraGlobalId, out var cameraGid))
            return "[[PROMPTRETURN]] FAILURE: The Global Object ID for the assigned camera you provided is not parsable";
        var assignedCamera = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(cameraGid) as CinemachineCamera;
        if (assignedCamera == null)
            return "[[PROMPTRETURN]] FAILURE: The CinemachineCamera from that GID is null";

        var cmTrack = timeline.GetOutputTracks()
            .OfType<CinemachineTrack>()
            .FirstOrDefault(t => t.name == trackName);

        if (cmTrack == null)
        {
            return "[[PROMPTRETURN]] FAILURE: No CinemachineTrack found with that name, the track was not created before trying to access its";
        }

        if (duration < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative duration";
        }
        if (startTime < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative startTime";
        }
        
        var shotClip = cmTrack.CreateClip<CinemachineShot>();
        shotClip.start = startTime;
        shotClip.duration = duration;
        
        var shot = shotClip.asset as CinemachineShot;
        director.SetReferenceValue(shot!.VirtualCamera.exposedName, assignedCamera);
        
        EditorUtility.SetDirty(timeline);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();
        
        return "[[PROMPTRETURN]] SUCCESS";
    }
    
    /// <param name="timelineName">exact name of the timeline</param>
    public static string CreateAnimationTrack(string timelineName, string animatedObjGlobalId, float startTime, float duration, string directorGlobalId)
    {
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }

        if (!TryParseGid(directorGlobalId, out var directorGid))
            return "[[PROMPTRETURN]] FAILURE: The Global Object ID for the director you provided is not parsable";
        var director = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(directorGid) as PlayableDirector;
        if (director == null)
            return "[[PROMPTRETURN]] FAILURE: The PlayableDirector from that GID is null";

        if (!TryParseGid(animatedObjGlobalId, out var animatedObjGid))
            return "[[PROMPTRETURN]] FAILURE: The Global Object ID for the animatedObj you provided is not parsable";

        var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(animatedObjGid);
        Transform animatedTransform = null;
        if (obj is GameObject go) animatedTransform = go.transform;
        else if (obj is Component comp) animatedTransform = comp.transform;

        if (animatedTransform == null)
            return "[[PROMPTRETURN]] FAILURE: Could not resolve a Transform from GID";       
        
        if (duration < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative duration";
        }
        if (startTime < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative startTime";
        }
        
        // track setup
        var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Camera Animation");
        var animator = animatedTransform.GetComponent<Animator>();
        if (animator == null) animator = animatedTransform.gameObject.AddComponent<Animator>();
        
        director.SetGenericBinding(animTrack, animator);
    
        AnimationClip clip = new AnimationClip();
        clip.name = animatedTransform.name;
        
        // order important
        // 1 add to timeline
        AssetDatabase.AddObjectToAsset(clip, timeline);

        // 2 create clip
        var animClip = animTrack.CreateClip(clip);
        animClip.start = startTime;
        animClip.duration = duration;
        // TODO: Change aswell, maybe ask user for name?
        animClip.displayName = animatedTransform.name;
    
        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(animatedTransform.gameObject);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        return "[[PROMPTRETURN]] SUCCESS";
    }

    // TODO: Add option to set Camera Target
    // TODO: Set option for FOV
    // TODO: Are these componentTypeNames accordingly converted to the types after???

    // TODO: I think keyframes need to be broken here
    // public static string SetAnimationTrackTrackingTarget(string timelinePath, string trackName, string animationClipName,
    //     List<(float dutchValue, float clipTime)> trackPositions)
    // {
    //     return SetAnimationTrackCurve(timelinePath, trackName, animationClipName, "CinemachineCamera", "Target.TrackingTarget",
    //         trackPositions);
    // }
    
    /// <param name="timelineName">exact name of the timeline</param>
    public static string SetAnimationTrackLensDutch(string timelineName, string trackName, string animationClipName, List<(float dutchValue, float clipTime)> trackPositions)
    {
        return SetAnimationTrackCurve(timelineName, trackName, animationClipName, "CinemachineCamera", "Lens.Dutch",
            trackPositions);
    }  
    /// <param name="timelineName">exact name of the timeline</param>
    public static string SetAnimationTrackSplinePosition(string timelineName, string trackName, string animationClipName, List<(float dutchValue, float clipTime)> trackPositions)
    {
        return SetAnimationTrackCurve(timelineName, trackName, animationClipName, "CinemachineSplineDolly", "m_SplineSettings.Position",
            trackPositions);
    }

    /// <param name="componentTypeName">GetType().ToString - used because we use reflection to assign some properties</param>
    /// <param name="propertyName">Name of the property that we are trying to access via reflection like Lens.dutch</param>
    /// <param name="trackPositions">                                                                                                                                             
    ///     List of (splinePosition, clipTime) tuples.
    ///     splinePosition: 0-1 value representing where on the spline.                                                                                                               
    ///     clipTime: time in seconds within the clip.
    /// </param>
    static string SetAnimationTrackCurve(string timelineName, string trackName, string animationClipName, string componentTypeName, string propertyName,  List<(float dutchValue, float clipTime)> trackPositions)
    {
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            // TODO: Maybe instruct to recall
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }
        
        var track = timeline.GetOutputTracks()
            .OfType<AnimationTrack>()
            .FirstOrDefault(t => t.name == trackName);

        if (track == null)
        {
            return "[[PROMPTRETURN]] FAILURE: No AnimationTrack found with that name, the track was not created before trying to access its";
        }
        
        if (trackPositions.Any(e => e.clipTime < 0))
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative track-position time";
        }

        // Dolly Position Curve
        AnimationCurve curve = new();
        foreach (var trackPosition in trackPositions)
        {
            curve.AddKey(trackPosition.clipTime, trackPosition.dutchValue);
        }

        var clip = track.GetClips()
            .FirstOrDefault(e => e.displayName == animationClipName);

        if (clip == null)
            return "Timeline Clip with the provided name wasnt found";
        
        var animClip = clip.animationClip;
        // "" means no parent
        if (animClip == null)
        {
            Debug.LogError("Clip is Null");
        }
        
        // TODO: If this doesnt work:
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == componentTypeName);
        
        if (type == null)
        {
            return "[[PROMPTRETURN]] FAILURE: to recreate the type from the string";
        }
        // TODO:
        // other solution:
        // AppDomain.CurrentDomain.GetAssemblies()
        // .SelectMany(a => a.GetTypes())                                                                                                                                 
        // .FirstOrDefault(t => t.Name == componentTypeName);
        animClip.SetCurve("", type, propertyName, curve);
        
        EditorUtility.SetDirty(animClip);
        EditorUtility.SetDirty(timeline); // because animclip is subasset of timeline
        AssetDatabase.SaveAssets();

        return "[[PROMPTRETURN]] SUCCESS";
    }
}