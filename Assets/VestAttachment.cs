using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class VestAttachment : MonoBehaviour
{
    public GameObject bodySourceManager;              // Kinect body tracking manager
    public GameObject[] vestPrefabs;                  // Array of 3D vest prefabs to choose from randomly

    public float vestScaleOffset = 1f;                // Manual scaling factor for fine-tuning size
    public Vector3 vestPositionOffset = new Vector3(0f, -0.1f, 0f); // Manual offset
    public Vector3 vestRotationOffset = new Vector3(0f, 180f, 0f);  // Manual rotation offset (X, Y, Z)

    private BodySourceManager _bodyManager;
    private Dictionary<ulong, GameObject> userVests = new Dictionary<ulong, GameObject>(); // Tracks anchors per user

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || vestPrefabs == null || vestPrefabs.Length == 0)
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

                // If user doesn't have a vest anchor, create one
                if (!userVests.ContainsKey(userId))
                {
                    GameObject vestAnchor = new GameObject("VestAnchor_" + userId);

                    //  Randomly pick a vest prefab
                    GameObject selectedVest = vestPrefabs[Random.Range(0, vestPrefabs.Length)];

                    GameObject vestModel = Instantiate(selectedVest, vestAnchor.transform);
                    vestModel.name = "VestModel";

                    userVests[userId] = vestAnchor;
                }

                // Find the skeleton GameObject
                GameObject skeleton = GameObject.Find("Body:" + userId);
                if (skeleton != null)
                {
                    Transform chestTransform = skeleton.transform.Find("SpineShoulder");
                    if (chestTransform != null)
                    {
                        GameObject vestAnchor = userVests[userId];
                        Transform vestModel = vestAnchor.transform.Find("VestModel");

                        // Dynamic scaling based on torso height
                        float scaleMultiplier = GetScaleMultiplier(body);
                        float finalScale = scaleMultiplier * vestScaleOffset;

                        // Parent the anchor to the chest joint
                        vestAnchor.transform.SetParent(chestTransform);

                        // Apply position and rotation offset
                        vestAnchor.transform.localPosition = vestPositionOffset;

                        Kinect.Vector4 orientation = body.JointOrientations[Kinect.JointType.SpineShoulder].Orientation;
                        Quaternion jointRotation = new Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);

                        Quaternion manualOffset = Quaternion.Euler(vestRotationOffset);
                        vestAnchor.transform.localRotation = jointRotation * manualOffset;

                        // Scale the model only
                        if (vestModel != null)
                        {
                            vestModel.localScale = Vector3.one * finalScale;
                        }
                    }
                }
            }
        }

        // Remove vests from users who left
        List<ulong> usersToRemove = new List<ulong>();
        foreach (ulong userId in userVests.Keys)
        {
            if (!activeUserIds.Contains(userId))
                usersToRemove.Add(userId);
        }

        foreach (ulong userId in usersToRemove)
        {
            Destroy(userVests[userId]);
            userVests.Remove(userId);
        }
    }

    private float GetScaleMultiplier(Kinect.Body body)
    {
        Kinect.Joint neck = body.Joints[Kinect.JointType.Neck];
        Kinect.Joint spineBase = body.Joints[Kinect.JointType.SpineBase];

        float torsoHeight = Mathf.Abs(neck.Position.Y - spineBase.Position.Y);
        float baseTorsoHeight = 0.5f; // Reference torso height for an average adult

        float scaleFactor = (torsoHeight / baseTorsoHeight) * 100f;

        return Mathf.Clamp(scaleFactor, 50f, 150f);
    }
}

