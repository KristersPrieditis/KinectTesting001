using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class JacketOutfitMapper : MonoBehaviour
{
    public GameObject bodySourceManager;
    public GameObject jacketPrefab;
    public float modelScaleFactor = 1.0f;
    public Vector3 jacketRotationOffset = new Vector3(0, 180, 0);
    public float zOffsetFromCamera = 75f;
    public float boneLengthMultiplier = 10f;

    private BodySourceManager _bodyManager;
    private Kinect.Body trackedBody;
    private GameObject activeJacket;

    private Dictionary<Kinect.JointType, string> boneMap = new Dictionary<Kinect.JointType, string>
    {
        { Kinect.JointType.SpineMid, "Spine" },
        { Kinect.JointType.ShoulderLeft, "LeftShoulder.L" },
        { Kinect.JointType.ElbowLeft, "LeftUpperArm.L" },
        { Kinect.JointType.WristLeft, "LeftForeArm.L" },
        { Kinect.JointType.ShoulderRight, "RightShoulder.R" },
        { Kinect.JointType.ElbowRight, "RightUpperArm.R" },
        { Kinect.JointType.WristRight, "RightForeArm.R" }
    };

    private Dictionary<Kinect.JointType, Transform> jacketBones = new Dictionary<Kinect.JointType, Transform>();

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || jacketPrefab == null) return;

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

        if (activeJacket == null)
            SpawnJacket();

        UpdateJacketBonePositions();
    }

    void SpawnJacket()
    {
        activeJacket = Instantiate(jacketPrefab);
        activeJacket.transform.position = Vector3.zero;
        activeJacket.transform.rotation = Quaternion.Euler(jacketRotationOffset);
        activeJacket.transform.localScale = Vector3.one * modelScaleFactor;
        InitializeJacketBones();
        Debug.Log("[JacketAttachment] Jacket spawned and initialized.");
    }

    void InitializeJacketBones()
    {
        jacketBones.Clear();

        Transform armature = activeJacket.transform.Find("Shirt_Armature");
        if (armature == null)
        {
            Debug.LogError("[JacketAttachment] 'Shirt_Armature' not found in jacket prefab!");
            return;
        }

        foreach (var pair in boneMap)
        {
            Transform bone = FindBoneRecursive(armature, pair.Value);
            if (bone != null)
            {
                jacketBones[pair.Key] = bone;
                Debug.Log($"[JacketAttachment] Mapped jacket bone: {pair.Value}");
            }
            else
            {
                Debug.LogWarning($"[JacketAttachment] Could not find jacket bone: {pair.Value}");
            }
        }
    }

    void UpdateJacketBonePositions()
    {
        Vector3 spinePosition = GetVector3FromJoint(trackedBody.Joints[Kinect.JointType.SpineMid]);
        activeJacket.transform.position = new Vector3(spinePosition.x, spinePosition.y, spinePosition.z + zOffsetFromCamera);

        foreach (var pair in jacketBones)
        {
            Kinect.JointType jointType = pair.Key;
            Transform jacketBone = pair.Value;

            Kinect.Joint source = trackedBody.Joints[jointType];
            Kinect.JointType? targetJointType = GetConnectedJoint(jointType);

            if (targetJointType.HasValue && jacketBone != null)
            {
                Kinect.Joint target = trackedBody.Joints[targetJointType.Value];
                Vector3 sourcePos = GetVector3FromJoint(source);
                Vector3 targetPos = GetVector3FromJoint(target);
                Vector3 direction = (targetPos - sourcePos).normalized;
                float boneLength = Vector3.Distance(sourcePos, targetPos) * boneLengthMultiplier;

                jacketBone.position = sourcePos + direction * boneLength;
                jacketBone.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private Kinect.JointType? GetConnectedJoint(Kinect.JointType joint)
    {
        switch (joint)
        {
            case Kinect.JointType.ElbowRight: return Kinect.JointType.WristRight;
            case Kinect.JointType.ElbowLeft: return Kinect.JointType.WristLeft;
            case Kinect.JointType.ShoulderRight: return Kinect.JointType.ElbowRight;
            case Kinect.JointType.ShoulderLeft: return Kinect.JointType.ElbowLeft;
            case Kinect.JointType.SpineMid: return Kinect.JointType.SpineShoulder;
            default: return null;
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

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X * 10f, joint.Position.Y * 10f, joint.Position.Z * 10f);
    }
}
