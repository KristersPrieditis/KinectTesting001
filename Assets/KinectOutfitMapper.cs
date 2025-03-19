using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class KinectOutfitMapper : MonoBehaviour
{
    public GameObject bodySourceManager;  // Kinect body tracking manager
    public GameObject outfitPrefab;       // The Mixamo rigged outfit
    public float modelScaleFactor = 1.0f; // Manual scaling adjustment

    private BodySourceManager _bodyManager;
    private GameObject activeOutfit;
    private Kinect.Body trackedBody;

    private Dictionary<Kinect.JointType, string> mixamoBoneMap = new Dictionary<Kinect.JointType, string>()
    {
        { Kinect.JointType.SpineBase, "mixamorig:Hips" },
        { Kinect.JointType.SpineMid, "mixamorig:Spine" },
        { Kinect.JointType.SpineShoulder, "mixamorig:Spine1" },
        { Kinect.JointType.Neck, "mixamorig:Neck" },
        { Kinect.JointType.Head, "mixamorig:Head" },

        { Kinect.JointType.ShoulderLeft, "mixamorig:LeftShoulder" },
        { Kinect.JointType.ElbowLeft, "mixamorig:LeftArm" },
        { Kinect.JointType.WristLeft, "mixamorig:LeftHand" },

        { Kinect.JointType.ShoulderRight, "mixamorig:RightShoulder" },
        { Kinect.JointType.ElbowRight, "mixamorig:RightArm" },
        { Kinect.JointType.WristRight, "mixamorig:RightHand" },

        { Kinect.JointType.HipLeft, "mixamorig:LeftUpLeg" },
        { Kinect.JointType.KneeLeft, "mixamorig:LeftLeg" },
        { Kinect.JointType.AnkleLeft, "mixamorig:LeftFoot" },

        { Kinect.JointType.HipRight, "mixamorig:RightUpLeg" },
        { Kinect.JointType.KneeRight, "mixamorig:RightLeg" },
        { Kinect.JointType.AnkleRight, "mixamorig:RightFoot" }
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

        // ** Only Scale Using modelScaleFactor**
        activeOutfit.transform.localScale = Vector3.one * modelScaleFactor;

        InitializeOutfitBones();
        Debug.Log("[KinectOutfitMapper] Outfit spawned!");
    }

    void InitializeOutfitBones()
    {
        outfitBones.Clear();

        // Find "Armature" first
        Transform armature = activeOutfit.transform.Find("Armature");
        if (armature == null)
        {
            Debug.LogError("[KinectOutfitMapper] ERROR: 'Armature' not found in the outfit rig!");
            return;
        }

        foreach (var pair in mixamoBoneMap)
        {
            Transform boneTransform = FindBoneRecursive(armature, pair.Value);
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
        foreach (Transform child in parent)
        {
            if (child.name == boneName)
                return child;

            Transform found = FindBoneRecursive(child, boneName);
            if (found != null)
                return found;
        }
        return null;
    }

    void UpdateOutfitTransform()
    {
        if (trackedBody == null) return;

        // **Set Root Position**
        Vector3 rootPosition = GetVector3FromJoint(trackedBody.Joints[Kinect.JointType.SpineBase]);
        activeOutfit.transform.position = rootPosition;

        // ** Only Scale Using modelScaleFactor**
        activeOutfit.transform.localScale = Vector3.one * modelScaleFactor;

        // ** Also Scale the Armature Properly**
        Transform armature = activeOutfit.transform.Find("Armature");
        if (armature != null)
        {
            armature.localScale = Vector3.one; // Keep armature scale 1:1 with outfit
        }

        // **Update Bone Positions**
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
        return new Vector3(joint.Position.X * 10, joint.Position.Y * 10, joint.Position.Z * 10);
    }
}
