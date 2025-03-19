using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class JacketAttachment : MonoBehaviour
{
    public GameObject bodySourceManager;  // Kinect body tracking manager
    public GameObject jacketPrefab;       // The 3D jacket prefab

    private BodySourceManager _bodyManager;
    private Dictionary<ulong, GameObject> userJackets = new Dictionary<ulong, GameObject>(); // Tracks jackets per user

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || jacketPrefab == null)
            return;

        Kinect.Body[] data = _bodyManager.GetData();
        if (data == null)
            return;

        List<ulong> activeUserIds = new List<ulong>();

        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                ulong userId = body.TrackingId;
                activeUserIds.Add(userId);

                // If user doesn't have a jacket, create one
                if (!userJackets.ContainsKey(userId))
                {
                    GameObject newJacket = Instantiate(jacketPrefab);
                    userJackets[userId] = newJacket;
                }

                // Attach the jacket to the skeleton
                AttachJacketToSkeleton(userJackets[userId], body, userId);
            }
        }

        // Remove jackets from users who left
        List<ulong> usersToRemove = new List<ulong>();
        foreach (ulong userId in userJackets.Keys)
        {
            if (!activeUserIds.Contains(userId))
                usersToRemove.Add(userId);
        }

        foreach (ulong userId in usersToRemove)
        {
            Destroy(userJackets[userId]);
            userJackets.Remove(userId);
        }
    }

    private void AttachJacketToSkeleton(GameObject jacket, Kinect.Body body, ulong userId)
    {
        // Find the skeleton GameObject
        GameObject skeleton = GameObject.Find("Body:" + userId);
        if (skeleton == null)
        {
            Debug.LogError($"[JacketAttachment] Skeleton for user {userId} not found!");
            return;
        }

        // **Find the "Shirt_Armature" first**
        Transform jacketArmature = jacket.transform.Find("Shirt_Armature");
        if (jacketArmature == null)
        {
            Debug.LogError("[JacketAttachment] 'Shirt_Armature' not found in jacket!");
            return;
        }

        // **Find Jacket Bones inside "Shirt_Armature"**
        Transform jacketSpine = jacketArmature.Find("Spine");
        Transform jacketLeftShoulder = jacketArmature.Find("LeftShoulder.L");
        Transform jacketLeftUpperArm = jacketArmature.Find("LeftUpperArm.L");
        Transform jacketLeftForearm = jacketArmature.Find("LeftForeArm.L");
        Transform jacketRightShoulder = jacketArmature.Find("RightShoulder.R");
        Transform jacketRightUpperArm = jacketArmature.Find("RightUpperArm.R");
        Transform jacketRightForearm = jacketArmature.Find("RightForeArm.R");

        // **Detach all bones from their parent hierarchy first**
        DetachBone(jacketLeftShoulder);
        DetachBone(jacketLeftUpperArm);
        DetachBone(jacketLeftForearm);
        DetachBone(jacketRightShoulder);
        DetachBone(jacketRightUpperArm);
        DetachBone(jacketRightForearm);

        // **Find Kinect Skeleton Joints**
        Transform spineJoint = skeleton.transform.Find("SpineMid");
        Transform leftShoulderJoint = skeleton.transform.Find("ShoulderLeft");
        Transform leftUpperArmJoint = skeleton.transform.Find("ElbowLeft");
        Transform leftForearmJoint = skeleton.transform.Find("WristLeft");
        Transform rightShoulderJoint = skeleton.transform.Find("ShoulderRight");
        Transform rightUpperArmJoint = skeleton.transform.Find("ElbowRight");
        Transform rightForearmJoint = skeleton.transform.Find("WristRight");

        // **Ensure all required bones are found before attaching**
        if (!spineJoint || !leftShoulderJoint || !rightShoulderJoint)
        {
            Debug.LogError($"[JacketAttachment] Some skeleton joints are missing for user {userId}!");
            return;
        }

        // **Scale dynamically BEFORE attaching**
        float scaleMultiplier = GetScaleMultiplier(body);
        jacket.transform.localScale = Vector3.one * scaleMultiplier;

        // **Attach each jacket bone to the skeleton**
        AttachBone(jacketSpine, spineJoint, "Spine");
        AttachBone(jacketLeftShoulder, leftShoulderJoint, "LeftShoulder.L");
        AttachBone(jacketLeftUpperArm, leftUpperArmJoint, "LeftUpperArm.L");
        AttachBone(jacketLeftForearm, leftForearmJoint, "LeftForeArm.L");
        AttachBone(jacketRightShoulder, rightShoulderJoint, "RightShoulder.R");
        AttachBone(jacketRightUpperArm, rightUpperArmJoint, "RightUpperArm.R");
        AttachBone(jacketRightForearm, rightForearmJoint, "RightForeArm.R");
    }

    private void DetachBone(Transform bone)
    {
        if (bone != null)
        {
            bone.SetParent(null); // **Detach from any hierarchy before re-parenting**
        }
    }


    public float jacketHeightOffset = -0.2f; // **Adjust this value in the Inspector to find the perfect fit**

    private void AttachBone(Transform jacketBone, Transform skeletonBone, string boneName)
    {
        if (jacketBone != null && skeletonBone != null)
        {
            jacketBone.SetParent(skeletonBone);
            jacketBone.localPosition = new Vector3(0, jacketHeightOffset, 0); // **Moves jacket down**
            jacketBone.localRotation = Quaternion.identity;
            Debug.Log($"[JacketAttachment] {boneName} successfully attached to {skeletonBone.name}");
        }
        else
        {
            Debug.LogWarning($"[JacketAttachment] Missing bone: {boneName} could not be attached.");
        }
    }

    private float GetScaleMultiplier(Kinect.Body body)
    {
        // Get shoulder width
        Kinect.Joint leftShoulder = body.Joints[Kinect.JointType.ShoulderLeft];
        Kinect.Joint rightShoulder = body.Joints[Kinect.JointType.ShoulderRight];
        Kinect.Joint spine = body.Joints[Kinect.JointType.SpineMid];

        float shoulderWidth = Mathf.Abs(leftShoulder.Position.X - rightShoulder.Position.X);
        float userDepth = spine.Position.Z; // Distance from Kinect

        // Scale factor
        float baseSize = 1.5f;
        float depthFactor = Mathf.Clamp(2.0f - userDepth, 0.5f, 2.5f);
        float scaleFactor = (shoulderWidth * 200f) * depthFactor;

        return Mathf.Clamp(scaleFactor, 10f, 300f); // Keep within reasonable limits
    }
}
