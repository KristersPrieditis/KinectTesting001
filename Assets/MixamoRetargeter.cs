using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MixamoRetargeter : MonoBehaviour
{
    [Header("Driver Rig (Kinect)")]
    // The intermediate (driver) rig that is driven by Kinect data.
    // If not assigned, the script will try to find one with tag "DriverRig".
    public Transform driverRoot;

    [Header("Final Rig Prefab & Spawn")]
    // The final visible rig prefab (with correct proportions).
    public GameObject finalRigPrefab;
    // The spawn point for the final rig.
    public Transform spawnPoint;
    // Once instantiated, this will be assigned.
    private Transform finalRoot;

    [Header("Bone Names (Matching in Both Rigs)")]
    public List<string> boneNames = new List<string>
    {
        "mixamorig:Hips",
        "mixamorig:Spine",
        "mixamorig:Spine1",
        "mixamorig:Neck",
        "mixamorig:Head",
        "mixamorig:LeftShoulder",
        "mixamorig:LeftArm",
        "mixamorig:LeftHand",
        "mixamorig:RightShoulder",
        "mixamorig:RightArm",
        "mixamorig:RightHand",
        "mixamorig:LeftUpLeg",
        "mixamorig:LeftLeg",
        "mixamorig:LeftFoot",
        "mixamorig:RightUpLeg",
        "mixamorig:RightLeg",
        "mixamorig:RightFoot"
    };

    [Header("Global Scaling")]
    [Tooltip("Uniform global scale for the final rig (e.g., 1 = original, 2 = double size).")]
    public float globalScale = 1f;

    [Header("Separate Scaling Multipliers")]
    [Tooltip("Multiplier for the visible skin (clothes, mesh) of the final rig.")]
    public Vector3 skinScaleMultiplier = Vector3.one;
    [Tooltip("Multiplier for the internal skeleton (bone lengths) of the final rig.")]
    public float boneScaleMultiplier = 1.0f;

    [Header("Final Model Adjustments")]
    public Vector3 finalPositionOffset = Vector3.zero;
    public Vector3 finalRotationOffset = Vector3.zero; // in Euler angles

    // Dictionaries to hold bone mappings: boneName -> driver transform, and boneName -> final transform.
    private Dictionary<string, Transform> driverBones = new Dictionary<string, Transform>();
    private Dictionary<string, Transform> finalBones = new Dictionary<string, Transform>();

    // Dictionary to store the original local bone lengths (from the final rig's T-pose).
    private Dictionary<string, float> finalBoneLengths = new Dictionary<string, float>();

    private bool isReady = false;

    void Start()
    {
        StartCoroutine(SetupRetargeting());
    }

    IEnumerator SetupRetargeting()
    {
        // Wait until driver rig is present.
        while (driverRoot == null)
        {
            GameObject driverObj = GameObject.FindWithTag("DriverRig");
            if (driverObj != null)
            {
                driverRoot = driverObj.transform;
                Debug.Log("[Retargeter] Found driver rig by tag.");
            }
            yield return null;
        }

        // Instantiate final rig if not pre-assigned.
        if (finalRigPrefab == null)
        {
            Debug.LogError("[Retargeter] No finalRigPrefab assigned!");
            yield break;
        }
        Vector3 spawnPos = spawnPoint ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

        GameObject finalInstance = Instantiate(finalRigPrefab, spawnPos, spawnRot);
        finalRoot = finalInstance.transform;
        // Set the final rig's scale to one (we will control scaling through our multipliers)
        finalRoot.localScale = Vector3.one;
        finalRoot.name = "FinalRigInstance";

        // Build bone mappings
        BuildBoneMappings();
        if (driverBones.Count == 0 || finalBones.Count == 0)
        {
            Debug.LogError("[Retargeter] Bone mapping failed. Check bone names.");
            yield break;
        }

        // Measure original bone lengths in final rig.
        MeasureFinalRigBoneLengths();

        isReady = true;
    }

    void BuildBoneMappings()
    {
        driverBones.Clear();
        finalBones.Clear();
        foreach (string boneName in boneNames)
        {
            Transform dBone = FindBoneRecursive(driverRoot, boneName);
            Transform fBone = FindBoneRecursive(finalRoot, boneName);
            if (dBone != null && fBone != null)
            {
                driverBones[boneName] = dBone;
                finalBones[boneName] = fBone;
                Debug.Log($"[Retargeter] Mapped bone: {boneName}");
            }
            else
            {
                Debug.LogWarning($"[Retargeter] Bone '{boneName}' not found in both driver and final rig.");
            }
        }
    }

    void MeasureFinalRigBoneLengths()
    {
        finalBoneLengths.Clear();
        foreach (string boneName in boneNames)
        {
            if (!finalBones.ContainsKey(boneName))
                continue;
            Transform boneT = finalBones[boneName];
            if (boneT == null)
                continue;
            Transform parentT = boneT.parent;
            if (parentT == null)
                continue;
            if (boneName == "mixamorig:Hips")
            {
                finalBoneLengths[boneName] = 0f;
                continue;
            }
            // Measure the bone length in the final rig's T-pose (local space)
            float dist = boneT.localPosition.magnitude;
            finalBoneLengths[boneName] = dist;
        }
    }

    void Update()
    {
        if (!isReady)
            return;

        // Update final rig's root transform relative to driver rig.
        finalRoot.position = driverRoot.position + finalPositionOffset;
        finalRoot.rotation = driverRoot.rotation * Quaternion.Euler(finalRotationOffset);
        // Apply the skin scaling multiplier to the final rig.
        finalRoot.localScale = Vector3.Scale(Vector3.one * globalScale, skinScaleMultiplier);

        // For each bone (except the root), update the position:
        foreach (string boneName in boneNames)
        {
            if (boneName == "mixamorig:Hips")
                continue;
            if (!driverBones.ContainsKey(boneName) || !finalBones.ContainsKey(boneName))
                continue;

            Transform driverBone = driverBones[boneName];
            Transform finalBone = finalBones[boneName];
            if (driverBone == null || finalBone == null)
                continue;

            Transform driverParent = driverBone.parent;
            Transform finalParent = finalBone.parent;
            if (driverParent == null || finalParent == null)
                continue;

            // Compute the driver bone's local direction (in driver rig local space)
            Vector3 driverLocalPos = driverBone.localPosition;
            float driverDistance = driverLocalPos.magnitude;
            if (driverDistance < 1e-5f)
                continue;
            Vector3 driverDir = driverLocalPos.normalized;

            // Get the final rig's original bone length (from T-pose)
            float baseLength = finalBoneLengths[boneName];
            // Scale that length by the globalScale factor.
            float newLength = baseLength * globalScale * boneScaleMultiplier;

            // Set the final bone's new local position relative to its parent:
            finalBone.localPosition = driverDir * newLength;
        }
    }

    // Recursive function to find a bone by name in the hierarchy.
    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent == null)
            return null;
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
}
