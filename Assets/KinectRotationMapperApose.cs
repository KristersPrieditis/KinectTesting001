using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

[System.Serializable]
public class BoneAdjustment
{
    [Tooltip("Kinect joint to adjust (maps to a Mixamo bone).")]
    public Kinect.JointType joint;
    [Tooltip("Extra manual rotation offset (in Euler angles) to apply to this bone.")]
    public Vector3 rotationOffsetEuler;
}

public class KinectRotationMapperApose : MonoBehaviour
{
    #region Inspector Variables

    [Header("Kinect / BodySourceManager")]
    [Tooltip("GameObject that contains the Kinect BodySourceManager component.")]
    public GameObject bodySourceManager;

    [Header("Mixamo Outfit Prefab (A-pose)")]
    [Tooltip("Prefab of the Mixamo rigged outfit (in A-pose).")]
    public GameObject outfitPrefab;

    [Header("Model Transform Adjustments")]
    [Tooltip("Position offset for the instantiated model (in world space).")]
    public Vector3 modelPositionOffset = Vector3.zero;
    [Tooltip("Rotation offset (Euler angles) for the instantiated model.")]
    public Vector3 modelRotationOffset = Vector3.zero;
    [Tooltip("Scale multiplier for the instantiated model.")]
    public Vector3 modelScaleMultiplier = Vector3.one;

    [Header("Global Rotation Correction")]
    [Tooltip("Global rotation correction (in Euler angles) to apply to each Kinect joint orientation.")]
    public Vector3 rotationCorrectionEuler = Vector3.zero;
    [Tooltip("If true, applies the above global rotation correction.")]
    public bool applyRotationCorrection = true;

    [Header("Axis Flip Settings")]
    [Tooltip("If true, negates Z and W components of the Kinect quaternion (a common fix).")]
    public bool useTypicalAxisFlip = false;

    [Header("Per-Bone Manual Adjustments")]
    [Tooltip("Adjust individual bone rotations (in Euler angles) that will be applied on top of the computed retargeting.")]
    public List<BoneAdjustment> boneAdjustments = new List<BoneAdjustment>();

    #endregion

    #region Internal Variables

    // Internal dictionary for per-bone manual corrections.
    private Dictionary<Kinect.JointType, Quaternion> boneCorrectionDict = new Dictionary<Kinect.JointType, Quaternion>();

    // The instantiated outfit.
    private GameObject outfitInstance;

    // Bind rotations for each bone (to align default local direction to +Y).
    private Dictionary<Kinect.JointType, Quaternion> bindRotations = new Dictionary<Kinect.JointType, Quaternion>();

    // Mapping from Kinect joint to Mixamo bone name.
    private Dictionary<Kinect.JointType, string> boneMap = new Dictionary<Kinect.JointType, string>()
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

    // Mapped bone transforms from the instantiated outfit.
    private Dictionary<Kinect.JointType, Transform> boneTransforms = new Dictionary<Kinect.JointType, Transform>();

    // Kinect tracking.
    private Kinect.Body trackedBody;
    private BodySourceManager _bodyManager;

    #endregion

    #region MonoBehaviour Methods

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();

        if (outfitPrefab != null)
        {
            // Instantiate the outfit and apply initial model adjustments.
            outfitInstance = Instantiate(outfitPrefab, Vector3.zero, Quaternion.identity);
            outfitInstance.name = "AposeMixamoOutfit";
            outfitInstance.transform.position += modelPositionOffset;
            outfitInstance.transform.rotation *= Quaternion.Euler(modelRotationOffset);
            outfitInstance.transform.localScale = modelScaleMultiplier;

            // Build bone mappings and compute bind rotations.
            InitializeBones();
            ComputeBindRotations();
            BuildBoneCorrections();
        }
        else
        {
            Debug.LogError("No outfitPrefab assigned!");
        }
    }

    void Update()
    {
        if (_bodyManager == null) return;

        Kinect.Body[] data = _bodyManager.GetData();
        if (data == null) return;

        // Find the first tracked body.
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

        // Update the overall model transform.
        outfitInstance.transform.position = modelPositionOffset;
        outfitInstance.transform.rotation = Quaternion.Euler(modelRotationOffset);
        outfitInstance.transform.localScale = modelScaleMultiplier;

        // Update each bone's rotation using Kinect data plus manual adjustments.
        UpdateBoneRotations();
    }

    #endregion

    #region Bone Mapping and Retargeting

    /// <summary>
    /// Initializes bone mapping by finding each bone in the instantiated outfit.
    /// Expects "mixamorig:Hips" as the root.
    /// </summary>
    private void InitializeBones()
    {
        boneTransforms.Clear();
        Transform hips = outfitInstance.transform.Find("mixamorig:Hips");
        if (hips == null)
        {
            Debug.LogError("mixamorig:Hips not found in the outfit!");
            return;
        }
        foreach (var pair in boneMap)
        {
            Kinect.JointType kJoint = pair.Key;
            string boneName = pair.Value;
            Transform boneT = FindBoneRecursive(hips, boneName);
            if (boneT != null)
            {
                boneTransforms[kJoint] = boneT;
                Debug.Log($"Bone {boneName} mapped to joint {kJoint}");
            }
            else
            {
                Debug.LogWarning($"Bone {boneName} not found in rig!");
            }
        }
    }

    /// <summary>
    /// Recursively finds a bone by name in the hierarchy.
    /// </summary>
    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent == null) return null;
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

    /// <summary>
    /// Computes the bind rotations for each bone. For each bone (except the root),
    /// it calculates the rotation needed to rotate its default local direction (from parent to bone)
    /// to point upward (+Y). This is used to reorient the bone so that Kinect orientations
    /// are applied on top of an expected "upward" default.
    /// </summary>
    private void ComputeBindRotations()
    {
        bindRotations.Clear();
        foreach (var pair in boneTransforms)
        {
            Kinect.JointType joint = pair.Key;
            Transform boneT = pair.Value;

            if (joint == Kinect.JointType.SpineBase)
            {
                bindRotations[joint] = Quaternion.identity;
                continue;
            }

            Transform parentT = boneT.parent;
            if (parentT == null)
            {
                bindRotations[joint] = Quaternion.identity;
                continue;
            }

            Vector3 localDir = boneT.localPosition.normalized;
            if (localDir.sqrMagnitude < 1e-6f)
            {
                bindRotations[joint] = Quaternion.identity;
                continue;
            }

            // Compute the rotation required to bring the bone's local direction to point +Y.
            Quaternion fromTo = Quaternion.FromToRotation(localDir, Vector3.up);
            bindRotations[joint] = fromTo;
        }
    }

    /// <summary>
    /// Builds a dictionary from the BoneAdjustments list.
    /// </summary>
    private void BuildBoneCorrections()
    {
        boneCorrectionDict.Clear();
        foreach (BoneAdjustment adj in boneAdjustments)
        {
            boneCorrectionDict[adj.joint] = Quaternion.Euler(adj.rotationOffsetEuler);
        }
    }

    /// <summary>
    /// Updates each bone's local rotation. For each bone:
    ///   1. Retrieves the Kinect joint orientation.
    ///   2. Optionally applies the typical axis flip.
    ///   3. Applies a global correction (if enabled).
    ///   4. Multiplies by the bind rotation.
    ///   5. Applies any per-bone manual correction.
    /// The result is assigned directly to the bone's localRotation.
    /// </summary>
    private void UpdateBoneRotations()
    {
        if (trackedBody == null) return;

        // Compute global correction from the Inspector.
        Quaternion globalCorrection = applyRotationCorrection ? Quaternion.Euler(rotationCorrectionEuler) : Quaternion.identity;

        foreach (var pair in boneTransforms)
        {
            Kinect.JointType joint = pair.Key;
            Transform boneT = pair.Value;
            if (boneT == null) continue;
            if (!trackedBody.JointOrientations.ContainsKey(joint)) continue;

            Kinect.JointOrientation jOrient = trackedBody.JointOrientations[joint];
            Quaternion kinectQ = new Quaternion(jOrient.Orientation.X,
                                                jOrient.Orientation.Y,
                                                jOrient.Orientation.Z,
                                                jOrient.Orientation.W);

            // Apply typical axis flip if enabled.
            if (useTypicalAxisFlip)
            {
                kinectQ = new Quaternion(kinectQ.x, kinectQ.y, -kinectQ.z, -kinectQ.w);
            }

            // Apply global correction.
            kinectQ = globalCorrection * kinectQ;

            // Calculate the final rotation: bindRotation * Kinect quaternion.
            Quaternion computedRotation = bindRotations.ContainsKey(joint) ? (bindRotations[joint] * kinectQ) : kinectQ;

            // Apply per-bone manual correction if any.
            if (boneCorrectionDict.ContainsKey(joint))
            {
                computedRotation = boneCorrectionDict[joint] * computedRotation;
            }

            // Assign the final rotation.
            boneT.localRotation = computedRotation;
        }
    }

    #endregion
}
