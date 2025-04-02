using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class KinectOutfitMapper : MonoBehaviour
{
    public GameObject bodySourceManager;  // Kinect body tracking manager
    public GameObject outfitPrefab;       // The Mixamo rigged outfit

    // Inspector field to move the entire model along the Z axis.
    public float zOffset = 0f;

    private BodySourceManager _bodyManager;
    private GameObject activeOutfit;
    private Kinect.Body trackedBody;

    private Dictionary<Kinect.JointType, string> mixamoBoneMap = new Dictionary<Kinect.JointType, string>()
    {
        { Kinect.JointType.SpineBase,      "mixamorig:Hips" },
        { Kinect.JointType.SpineMid,       "mixamorig:Spine" },
        { Kinect.JointType.SpineShoulder,  "mixamorig:Spine1" },
        { Kinect.JointType.Neck,           "mixamorig:Neck" },
        { Kinect.JointType.Head,           "mixamorig:Head" },

        { Kinect.JointType.ShoulderLeft,   "mixamorig:LeftShoulder" },
        { Kinect.JointType.ElbowLeft,      "mixamorig:LeftArm" },
        { Kinect.JointType.WristLeft,      "mixamorig:LeftHand" },

        { Kinect.JointType.ShoulderRight,  "mixamorig:RightShoulder" },
        { Kinect.JointType.ElbowRight,     "mixamorig:RightArm" },
        { Kinect.JointType.WristRight,     "mixamorig:RightHand" },

        { Kinect.JointType.HipLeft,        "mixamorig:LeftUpLeg" },
        { Kinect.JointType.KneeLeft,       "mixamorig:LeftLeg" },
        { Kinect.JointType.AnkleLeft,      "mixamorig:LeftFoot" },

        { Kinect.JointType.HipRight,       "mixamorig:RightUpLeg" },
        { Kinect.JointType.KneeRight,      "mixamorig:RightLeg" },
        { Kinect.JointType.AnkleRight,     "mixamorig:RightFoot" }
    };

    private Dictionary<Kinect.JointType, Transform> outfitBones = new Dictionary<Kinect.JointType, Transform>();

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || outfitPrefab == null)
            return;

        Kinect.Body[] data = _bodyManager.GetData();
        if (data == null)
            return;

        trackedBody = null;
        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                trackedBody = body;
                break;
            }
        }

        if (trackedBody == null)
        {
            Debug.Log("[KinectOutfitMapper] No tracked body detected.");
            return;
        }

        if (activeOutfit == null)
        {
            SpawnOutfit();
        }

        UpdateOutfitTransform();
    }

    void SpawnOutfit()
    {
        activeOutfit = Instantiate(outfitPrefab);
        activeOutfit.transform.position = Vector3.zero;
        activeOutfit.transform.rotation = Quaternion.identity;
        activeOutfit.transform.localScale = Vector3.one; // Forcing a reset scale

        InitializeOutfitBones();
        Debug.Log("[KinectOutfitMapper] Outfit spawned!");
    }

    void InitializeOutfitBones()
    {
        outfitBones.Clear();

        // Use "mixamorig:Hips" as the root instead of "Armature".
        Transform hips = activeOutfit.transform.Find("mixamorig:Hips");
        if (hips == null)
        {
            Debug.LogError("[KinectOutfitMapper] ERROR: 'mixamorig:Hips' not found in the outfit rig!");
            return;
        }

        foreach (var pair in mixamoBoneMap)
        {
            Transform boneTransform = FindBoneRecursive(hips, pair.Value);
            if (boneTransform != null)
            {
                outfitBones[pair.Key] = boneTransform;
                Debug.Log($"[KinectOutfitMapper] Bone mapped: {pair.Value}");
            }
            else
            {
                Debug.LogWarning($"[KinectOutfitMapper] WARNING: Bone '{pair.Value}' not found in Mixamo rig.");
            }
        }
    }

    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindBoneRecursive(child, boneName);
            if (found != null)
                return found;
        }
        return null;
    }

    void UpdateOutfitTransform()
    {
        // Set Root Position: Use the Kinect SpineBase joint and apply the zOffset.
        Vector3 rootPosition = GetVector3FromJoint(trackedBody.Joints[Kinect.JointType.SpineBase]);
        activeOutfit.transform.position = rootPosition;

        // Update each bone's position to mimic the Kinect skeleton.
        foreach (var pair in outfitBones)
        {
            Kinect.JointType kinectJoint = pair.Key;
            Transform boneTransform = pair.Value;
            if (boneTransform == null)
                continue;

            Vector3 targetPosition = GetVector3FromJoint(trackedBody.Joints[kinectJoint]);
            boneTransform.position = targetPosition;
        }
    }

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X * 10f,
                           joint.Position.Y * 10f,
                           (joint.Position.Z * 10f) + zOffset);
    }
}