using System.Collections.Generic;
using UnityEngine;

public class KinectToMixamoBridge : MonoBehaviour
{
    [Header("References")]
    public GameObject kinectSkeletonRoot;
    public GameObject mixamoRoot;

    [Header("Movement Tuning")]
    public float globalMovementScale = 5f;
    public float rotationLerpSpeed = 10f;

    [Header("Flip Controls")]
    public bool invertMovement = true;
    public bool invertRotation = true;

    private class BonePair
    {
        public string kinectName;
        public string mixamoName;
        public float movementScale;
        public KinectJointTracker tracker;
        public MixamoMotionReceiver receiver;

        public string kinectChildName;   // For rotation
        public Transform mixamoBone;
    }

    private List<BonePair> bonePairs = new List<BonePair>();

    private Dictionary<string, string> boneMap = new Dictionary<string, string>()
    {
        { "SpineBase", "mixamorig:Hips" },
        { "SpineMid", "mixamorig:Spine" },
        { "SpineShoulder", "mixamorig:Spine2" },
        { "Neck", "mixamorig:Neck" },
        { "Head", "mixamorig:Head" },

        { "ShoulderLeft", "mixamorig:LeftShoulder" },
        { "ElbowLeft", "mixamorig:LeftArm" },
        { "WristLeft", "mixamorig:LeftForeArm" },

        { "ShoulderRight", "mixamorig:RightShoulder" },
        { "ElbowRight", "mixamorig:RightArm" },
        { "WristRight", "mixamorig:RightForeArm" },

        { "HipLeft", "mixamorig:LeftUpLeg" },
        { "KneeLeft", "mixamorig:LeftLeg" },
        { "AnkleLeft", "mixamorig:LeftFoot" },

        { "HipRight", "mixamorig:RightUpLeg" },
        { "KneeRight", "mixamorig:RightLeg" },
        { "AnkleRight", "mixamorig:RightFoot" },
    };

    private Dictionary<string, string> rotationPairs = new Dictionary<string, string>()
    {
        { "ShoulderRight", "ElbowRight" },
        { "ElbowRight", "WristRight" },
        { "ShoulderLeft", "ElbowLeft" },
        { "ElbowLeft", "WristLeft" },
        { "HipRight", "KneeRight" },
        { "KneeRight", "AnkleRight" },
        { "HipLeft", "KneeLeft" },
        { "KneeLeft", "AnkleLeft" }
    };

    private bool mappingComplete = false;

    void Start()
    {
        SetupMixamoReceivers();
    }

    void Update()
    {
        TryKinectMapping();

        if (!mappingComplete) return;

        foreach (var pair in bonePairs)
        {
            if (pair.tracker == null || pair.receiver == null) continue;

            bool isHips = pair.kinectName == "SpineBase";
            Vector3 delta = isHips ? pair.tracker.WorldDelta : pair.tracker.LocalDelta;

            if (invertMovement)
                delta *= -1f;

            pair.receiver.ApplyDelta(delta, pair.movementScale * globalMovementScale);

            if (!string.IsNullOrEmpty(pair.kinectChildName) && pair.mixamoBone != null)
            {
                ApplyBoneRotation(pair);
            }
        }
    }

    private void ApplyBoneRotation(BonePair pair)
    {
        Transform parentJoint = RecursiveFind(kinectSkeletonRoot.transform, pair.kinectName);
        Transform childJoint = RecursiveFind(kinectSkeletonRoot.transform, pair.kinectChildName);
        if (parentJoint == null || childJoint == null) return;

        Vector3 targetDirection = (childJoint.position - parentJoint.position).normalized;
        if (invertRotation)
            targetDirection *= -1f;

        Vector3 currentDirection = pair.mixamoBone.forward;
        Quaternion targetRotation = Quaternion.FromToRotation(currentDirection, targetDirection) * pair.mixamoBone.rotation;
        pair.mixamoBone.rotation = Quaternion.Slerp(pair.mixamoBone.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
    }

    private void SetupMixamoReceivers()
    {
        if (mixamoRoot == null)
        {
            var hips = GameObject.Find("mixamorig:Hips");
            if (hips != null)
            {
                mixamoRoot = hips;
                Debug.Log("Auto-assigned Mixamo root: mixamorig:Hips");
            }
            else
            {
                Debug.LogWarning("Mixamo root not found.");
                return;
            }
        }

        Debug.Log("Preparing Mixamo bones...");

        bonePairs.Clear();

        foreach (var kvp in boneMap)
        {
            string kinectName = kvp.Key;
            string mixamoName = kvp.Value;

            Transform mixamoBone = RecursiveFind(mixamoRoot.transform, mixamoName);
            if (mixamoBone == null)
            {
                Debug.LogWarning("Missing Mixamo bone: " + mixamoName);
                continue;
            }

            var receiver = mixamoBone.GetComponent<MixamoMotionReceiver>();
            if (receiver == null)
                receiver = mixamoBone.gameObject.AddComponent<MixamoMotionReceiver>();

            bonePairs.Add(new BonePair
            {
                kinectName = kinectName,
                mixamoName = mixamoName,
                mixamoBone = mixamoBone,
                receiver = receiver,
                movementScale = 1f,
                kinectChildName = rotationPairs.ContainsKey(kinectName) ? rotationPairs[kinectName] : null
            });

            Debug.Log("Prepared Mixamo bone: " + mixamoName);
        }
    }

    private void TryKinectMapping()
    {
        if (mappingComplete)
            return;

        if (kinectSkeletonRoot == null)
        {
            var injectors = Object.FindObjectsByType<JointTrackerInjector>(FindObjectsSortMode.None);
            if (injectors.Length > 0)
            {
                kinectSkeletonRoot = injectors[0].gameObject;
                Debug.Log("Auto-assigned Kinect skeleton root.");
            }
        }

        if (kinectSkeletonRoot == null || mixamoRoot == null)
            return;

        Transform test = RecursiveFind(kinectSkeletonRoot.transform, "ElbowRight");
        if (test == null)
            return;

        Debug.Log("Assigning Kinect trackers to Mixamo...");

        foreach (var pair in bonePairs)
        {
            Transform joint = RecursiveFind(kinectSkeletonRoot.transform, pair.kinectName);
            if (joint == null)
            {
                Debug.LogWarning("Missing Kinect joint: " + pair.kinectName);
                continue;
            }

            var tracker = joint.GetComponent<KinectJointTracker>();
            if (tracker == null)
            {
                Debug.LogWarning("Missing KinectJointTracker on: " + pair.kinectName);
                continue;
            }

            pair.tracker = tracker;

            Debug.Log("Mapped " + pair.kinectName + " to " + pair.mixamoName);
        }

        mappingComplete = true;
        Debug.Log("Kinect to Mixamo mapping complete.");
    }

    private Transform RecursiveFind(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        foreach (Transform child in parent)
        {
            var result = RecursiveFind(child, name);
            if (result != null)
                return result;
        }

        return null;
    }
}
