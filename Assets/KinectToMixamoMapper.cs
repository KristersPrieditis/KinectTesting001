using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class KinectToMixamoMapper : MonoBehaviour
{
    [Header("Kinect / BodySourceManager")]
    public GameObject bodySourceManager;

    [Header("Mixamo Character Prefab")]
    public GameObject mixamoPrefab;

    [Header("Offset & Scale Controls")]
    public Vector3 modelOffset = Vector3.zero;
    public float kinectScale = 10f; // Kinect is in meters, Unity often in decimeters/centimeters

    private GameObject mixamoInstance;
    private BodySourceManager _bodyManager;
    private Kinect.Body trackedBody;

    // Kinect -> Mixamo joint map
    private Dictionary<Kinect.JointType, string> jointToBoneMap = new Dictionary<Kinect.JointType, string>()
    {
        { Kinect.JointType.SpineBase, "mixamorig:Hips" },
        { Kinect.JointType.SpineMid, "mixamorig:Spine" },
        { Kinect.JointType.SpineShoulder, "mixamorig:Spine1" },
        { Kinect.JointType.ShoulderLeft, "mixamorig:LeftShoulder" },
        { Kinect.JointType.ElbowLeft, "mixamorig:LeftArm" },
        { Kinect.JointType.ShoulderRight, "mixamorig:RightShoulder" },
        { Kinect.JointType.ElbowRight, "mixamorig:RightArm" },
        { Kinect.JointType.HipLeft, "mixamorig:LeftUpLeg" },
        { Kinect.JointType.KneeLeft, "mixamorig:LeftLeg" },
        { Kinect.JointType.HipRight, "mixamorig:RightUpLeg" },
        { Kinect.JointType.KneeRight, "mixamorig:RightLeg" },
    };

    private Dictionary<Kinect.JointType, Transform> mixamoBones = new Dictionary<Kinect.JointType, Transform>();

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();

        if (mixamoPrefab != null)
        {
            mixamoInstance = Instantiate(mixamoPrefab, Vector3.zero, Quaternion.identity);
            mixamoInstance.name = "MixamoCharacter";
            InitializeMixamoBones();
        }
    }

    void Update()
    {
        if (_bodyManager == null || mixamoInstance == null) return;

        Kinect.Body[] data = _bodyManager.GetData();
        if (data == null) return;

        trackedBody = null;
        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                trackedBody = body;
                break;
            }
        }

        if (trackedBody == null) return;

        // Apply bone positions
        foreach (var pair in jointToBoneMap)
        {
            Kinect.JointType kinectJoint = pair.Key;
            string mixamoBoneName = pair.Value;

            if (!trackedBody.Joints.ContainsKey(kinectJoint)) continue;
            if (!mixamoBones.ContainsKey(kinectJoint)) continue;

            Kinect.Joint joint = trackedBody.Joints[kinectJoint];
            Vector3 jointPosition = new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z) * kinectScale;
            jointPosition += modelOffset;

            mixamoBones[kinectJoint].position = jointPosition;
        }
    }

    void InitializeMixamoBones()
    {
        mixamoBones.Clear();
        Transform root = mixamoInstance.transform.Find("mixamorig:Hips");
        if (root == null)
        {
            Debug.LogError("[MixamoMapper] Could not find mixamorig:Hips");
            return;
        }

        foreach (var pair in jointToBoneMap)
        {
            Kinect.JointType joint = pair.Key;
            string boneName = pair.Value;
            Transform bone = FindBoneRecursive(root, boneName);

            if (bone != null)
            {
                mixamoBones[joint] = bone;
                Debug.Log($"Mapped {boneName} to {joint}");
            }
            else
            {
                Debug.LogWarning($"Could not find {boneName} in Mixamo rig");
            }
        }
    }

    Transform FindBoneRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindBoneRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

