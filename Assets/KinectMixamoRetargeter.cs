using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

[System.Serializable]
public class BoneOffset
{
    public Kinect.JointType jointType;
    public Vector3 eulerOffset; // Per-bone Euler offset (in degrees) for fine-tuning
}

public class KinectMixamoRetargeter : MonoBehaviour
{
    [Header("Kinect + Outfit References")]
    public GameObject bodySourceManager;   // Kinect body tracking manager
    public GameObject outfitPrefab;        // Mixamo rigged character prefab

    [Header("Scale & Position")]
    public float modelScaleFactor = 1.0f;    // Overall model scale
    public float positionScaleFactor = 1.0f; // Scale factor for converting Kinect positions to Unity space
    public Vector3 modelPositionOffset = Vector3.zero; // Shift the model in the scene

    [Header("Coordinate System Tweaks")]
    public bool flipX = true; // Flip X-axis if needed
    public bool flipZ = true; // Flip Z-axis if needed

    [Header("Global & Per-Bone Offsets")]
    public Quaternion globalRotationOffset = Quaternion.identity; // Global rotation correction
    public BoneOffset[] boneOffsets; // Per-bone Euler offsets (set these in the Inspector)

    // Mapping from Kinect joints to Mixamo rig bone names.
    // Adjust these names to exactly match your Mixamo character.
    private Dictionary<Kinect.JointType, string> bodyBoneMap = new Dictionary<Kinect.JointType, string>()
    {
        // Torso (note: Mixamo “Swat” usually has Hips -> Spine -> Spine1 -> Spine2 -> Neck -> Head)
        // One common mapping is to use:
        // Kinect SpineBase  mixamorig:Hips
        // Kinect SpineMid   mixamorig:Spine
        // Kinect SpineShoulder  mixamorig:Spine2   (skipping Spine1)
        // Kinect Neck  mixamorig:Neck
        // Kinect Head  mixamorig:Head
        { Kinect.JointType.SpineBase,      "mixamorig:Hips" },
        { Kinect.JointType.SpineMid,       "mixamorig:Spine" },
        { Kinect.JointType.SpineShoulder,  "mixamorig:Spine2" },
        { Kinect.JointType.Neck,           "mixamorig:Neck" },
        { Kinect.JointType.Head,           "mixamorig:Head" },

        // Left Arm
        { Kinect.JointType.ShoulderLeft,   "mixamorig:LeftShoulder" },
        { Kinect.JointType.ElbowLeft,      "mixamorig:LeftArm" },
        { Kinect.JointType.WristLeft,      "mixamorig:LeftForeArm" },

        // Right Arm
        { Kinect.JointType.ShoulderRight,  "mixamorig:RightShoulder" },
        { Kinect.JointType.ElbowRight,     "mixamorig:RightArm" },
        { Kinect.JointType.WristRight,     "mixamorig:RightForeArm" },

        // Left Leg
        { Kinect.JointType.HipLeft,        "mixamorig:LeftUpLeg" },
        { Kinect.JointType.KneeLeft,       "mixamorig:LeftLeg" },
        { Kinect.JointType.AnkleLeft,      "mixamorig:LeftFoot" },

        // Right Leg
        { Kinect.JointType.HipRight,       "mixamorig:RightUpLeg" },
        { Kinect.JointType.KneeRight,      "mixamorig:RightLeg" },
        { Kinect.JointType.AnkleRight,     "mixamorig:RightFoot" }
    };

    // Stores the mapped Mixamo bones.
    private Dictionary<Kinect.JointType, Transform> outfitBones = new Dictionary<Kinect.JointType, Transform>();
    // Stores each bone’s initial (rest) local rotation.
    private Dictionary<Kinect.JointType, Quaternion> initialLocalRotations = new Dictionary<Kinect.JointType, Quaternion>();

    private BodySourceManager _bodyManager;
    private GameObject activeOutfit;
    private Kinect.Body trackedBody;

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

        // Find the first tracked body
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
            Debug.Log("[KinectMixamoRetargeter] No tracked body detected.");
            return;
        }

        if (activeOutfit == null)
            SpawnOutfit();

        UpdateOutfitTransform();
        UpdateBoneRotations();
    }

    void SpawnOutfit()
    {
        activeOutfit = Instantiate(outfitPrefab);
        activeOutfit.transform.position = Vector3.zero;
        activeOutfit.transform.rotation = Quaternion.identity;
        activeOutfit.transform.localScale = Vector3.one * modelScaleFactor;

        InitializeOutfitBones();
        Debug.Log("[KinectMixamoRetargeter] Outfit spawned!");
    }

    void InitializeOutfitBones()
    {
        outfitBones.Clear();
        initialLocalRotations.Clear();

        // For Mixamo rigs, try to find an "Armature" child. If not, use the root.
        Transform armature = activeOutfit.transform.Find("Armature");
        if (armature == null)
        {
            Debug.LogWarning("[KinectMixamoRetargeter] 'Armature' not found. Using root transform for bone mapping.");
            armature = activeOutfit.transform;
        }

        foreach (var pair in bodyBoneMap)
        {
            Transform boneTransform = FindBoneRecursive(armature, pair.Value);
            if (boneTransform != null)
            {
                outfitBones[pair.Key] = boneTransform;
                initialLocalRotations[pair.Key] = boneTransform.localRotation;
                Debug.Log("[KinectMixamoRetargeter] Bone mapped: " + pair.Value);
            }
            else
            {
                Debug.LogWarning("[KinectMixamoRetargeter] WARNING: Bone '" + pair.Value + "' not found.");
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
        if (trackedBody == null)
            return;

        // Position the entire outfit using Kinect's SpineBase joint plus a user-defined offset.
        Vector3 rootPosition = GetVector3FromJoint(trackedBody.Joints[Kinect.JointType.SpineBase]) + modelPositionOffset;
        activeOutfit.transform.position = rootPosition;
        activeOutfit.transform.localScale = Vector3.one * modelScaleFactor;
    }

    // Update each Mixamo bone's rotation by computing a direction from the Kinect joint positions.
    void UpdateBoneRotations()
    {
        if (trackedBody == null)
            return;

        foreach (var pair in outfitBones)
        {
            Kinect.JointType jointType = pair.Key;
            Transform bone = pair.Value;
            if (bone == null)
                continue;

            // Get the Kinect joint and its designated connected (child) joint.
            Kinect.Joint sourceJoint = trackedBody.Joints[jointType];
            Kinect.JointType? childJointType = GetConnectedJoint(jointType);
            if (childJointType == null)
                continue;
            Kinect.Joint targetJoint = trackedBody.Joints[childJointType.Value];

            // Compute the direction vector from source to target in world space.
            Vector3 sourcePos = GetVector3FromJoint(sourceJoint);
            Vector3 targetPos = GetVector3FromJoint(targetJoint);
            Vector3 worldDir = (targetPos - sourcePos).normalized;
            if (worldDir == Vector3.zero)
                continue;

            // Apply axis flips if necessary.
            if (flipX) worldDir.x = -worldDir.x;
            if (flipZ) worldDir.z = -worldDir.z;

            // Convert the world-space direction to the local space of the bone's parent.
            Quaternion parentInvRotation = bone.parent ? Quaternion.Inverse(bone.parent.rotation) : Quaternion.identity;
            Vector3 localDir = parentInvRotation * worldDir;

            // Compute a rotation that aligns the bone's forward (local Z axis) with the computed local direction.
            Quaternion targetLocalRotation = Quaternion.LookRotation(localDir, Vector3.up);

            // Get any per-bone offset.
            Quaternion perBoneOffset = GetBoneOffsetRotation(jointType);

            // Combine: global offset, per-bone offset, the computed rotation, and the initial rest pose.
            bone.localRotation = globalRotationOffset * perBoneOffset * targetLocalRotation * initialLocalRotations[jointType];
        }
    }

    // Determines which Kinect joint to use as the "child" for computing a direction.
    Kinect.JointType? GetConnectedJoint(Kinect.JointType joint)
    {
        switch (joint)
        {
            case Kinect.JointType.SpineBase: return Kinect.JointType.SpineMid;
            case Kinect.JointType.SpineMid: return Kinect.JointType.SpineShoulder;
            case Kinect.JointType.SpineShoulder: return Kinect.JointType.Neck;
            case Kinect.JointType.Neck: return Kinect.JointType.Head;
            case Kinect.JointType.ShoulderLeft: return Kinect.JointType.ElbowLeft;
            case Kinect.JointType.ElbowLeft: return Kinect.JointType.WristLeft;
            case Kinect.JointType.WristLeft: return null;
            case Kinect.JointType.ShoulderRight: return Kinect.JointType.ElbowRight;
            case Kinect.JointType.ElbowRight: return Kinect.JointType.WristRight;
            case Kinect.JointType.WristRight: return null;
            case Kinect.JointType.HipLeft: return Kinect.JointType.KneeLeft;
            case Kinect.JointType.KneeLeft: return Kinect.JointType.AnkleLeft;
            case Kinect.JointType.AnkleLeft: return null;
            case Kinect.JointType.HipRight: return Kinect.JointType.KneeRight;
            case Kinect.JointType.KneeRight: return Kinect.JointType.AnkleRight;
            case Kinect.JointType.AnkleRight: return null;
            default: return null;
        }
    }

    private Quaternion GetBoneOffsetRotation(Kinect.JointType jointType)
    {
        BoneOffset offsetEntry = System.Array.Find(boneOffsets, b => b.jointType == jointType);
        if (offsetEntry != null)
            return Quaternion.Euler(offsetEntry.eulerOffset);
        return Quaternion.identity;
    }

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(
            joint.Position.X * positionScaleFactor,
            joint.Position.Y * positionScaleFactor,
            joint.Position.Z * positionScaleFactor
        );
    }
}
