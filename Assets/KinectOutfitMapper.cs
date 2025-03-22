using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class KinectOutfitMapper : MonoBehaviour
{
    public GameObject bodySourceManager;   // Kinect body tracking manager
    public GameObject outfitPrefab;        // Your new rigged character
    public float modelScaleFactor = 1.0f;    // Manual scaling adjustment

    private BodySourceManager _bodyManager;
    private GameObject activeOutfit;
    private Kinect.Body trackedBody;

    // Map Kinect joints to your model’s bone names (adjust these to match your rig exactly)
    private Dictionary<Kinect.JointType, string> bodyBoneMap = new Dictionary<Kinect.JointType, string>()
    {
        // Torso
        { Kinect.JointType.SpineBase,      "Hip" },
        { Kinect.JointType.SpineMid,       "Spine" },
        { Kinect.JointType.SpineShoulder,  "Neck" },
        { Kinect.JointType.Neck,           "Neck" },
        { Kinect.JointType.Head,           "Head" },

        // Left Arm
        { Kinect.JointType.ShoulderLeft,   "LeftShoulder" },
        { Kinect.JointType.ElbowLeft,      "LeftUpperArm" },
        { Kinect.JointType.WristLeft,      "LeftHand" },

        // Right Arm
        { Kinect.JointType.ShoulderRight,  "RightShoulder" },
        { Kinect.JointType.ElbowRight,     "RightUpperArm" },
        { Kinect.JointType.WristRight,     "RightHand" },

        // Left Leg
        { Kinect.JointType.HipLeft,        "LeftUpperLeg" },
        { Kinect.JointType.KneeLeft,       "LeftLowerLeg" },
        { Kinect.JointType.AnkleLeft,      "LeftFoot" },

        // Right Leg
        { Kinect.JointType.HipRight,       "RightUpperLeg" },
        { Kinect.JointType.KneeRight,      "RightLowerLeg" },
        { Kinect.JointType.AnkleRight,     "RightFoot" }
    };

    private Dictionary<Kinect.JointType, Transform> outfitBones = new Dictionary<Kinect.JointType, Transform>();

    void Start()
    {
        if (bodySourceManager != null)
        {
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
        }
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
            Debug.Log("[KinectOutfitMapper] No tracked body detected.");
            return;
        }

        // Spawn the outfit if not already spawned
        if (activeOutfit == null)
        {
            SpawnOutfit();
        }

        // Update the root transform (position) and then update each bone’s rotation
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
        Debug.Log("[KinectOutfitMapper] Outfit spawned!");
    }

    void InitializeOutfitBones()
    {
        outfitBones.Clear();

        // Adjust this if your rig’s Armature is structured differently.
        Transform armature = activeOutfit.transform.Find("Armature");
        if (armature == null)
        {
            Debug.LogError("[KinectOutfitMapper] ERROR: 'Armature' not found in the outfit!");
            return;
        }

        // Map each Kinect joint to its corresponding bone in the rig
        foreach (var pair in bodyBoneMap)
        {
            Transform boneTransform = FindBoneRecursive(armature, pair.Value);
            if (boneTransform != null)
            {
                outfitBones[pair.Key] = boneTransform;
                Debug.Log($"[KinectOutfitMapper] Bone mapped: {pair.Value}");
            }
            else
            {
                Debug.LogWarning($"[KinectOutfitMapper] WARNING: Bone '{pair.Value}' not found.");
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

        // Position the entire outfit at the user's SpineBase
        Vector3 rootPosition = GetVector3FromJoint(trackedBody.Joints[Kinect.JointType.SpineBase]);
        activeOutfit.transform.position = rootPosition;

        // Ensure the outfit maintains the correct scale
        activeOutfit.transform.localScale = Vector3.one * modelScaleFactor;
    }

    // Update each bone’s rotation using Kinect’s JointOrientation data
    void UpdateBoneRotations()
    {
        if (trackedBody == null)
            return;

        foreach (var pair in outfitBones)
        {
            Kinect.JointType jointType = pair.Key;
            Transform boneTransform = pair.Value;
            if (boneTransform == null)
                continue;

            // 1) Retrieve the Kinect joint orientation
            Kinect.JointOrientation jointOrientation = trackedBody.JointOrientations[jointType];

            // 2) Convert Kinect’s Vector4 (quaternion) into a Unity Quaternion
            Quaternion kinectRotation = new Quaternion(
                jointOrientation.Orientation.X,
                jointOrientation.Orientation.Y,
                jointOrientation.Orientation.Z,
                jointOrientation.Orientation.W
            );

            // 3) Adjust for coordinate system differences between Kinect and Unity.
            //    The following example flips the X and Z axes. Adjust these as necessary.
            Quaternion unityRotation = new Quaternion(
                -kinectRotation.x, // Flip X
                 kinectRotation.y,
                -kinectRotation.z, // Flip Z
                 kinectRotation.w
            );

            // 4) Optionally, apply a rest pose offset here if needed (e.g., offset * unityRotation)
            boneTransform.localRotation = unityRotation;
        }
    }

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        // Convert Kinect joint position to a more Unity-friendly scale.
        return new Vector3(
            joint.Position.X * 10f,
            joint.Position.Y * 10f,
            joint.Position.Z * 10f
        );
    }
}
