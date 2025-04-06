using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class HatAttachment : MonoBehaviour
{
    public GameObject bodySourceManager;                  // Kinect body tracking manager
    public GameObject[] hatPrefabs;                       // Array of 3D hat prefabs to choose from

    public float hatScaleOffset = 1f;                     // Manual scale multiplier
    public Vector3 hatPositionOffset = new Vector3(0f, 0.1f, 0f); // Slightly above the head
    public float hatRotationY = 180f;                     // Manual Y-axis rotation offset

    private BodySourceManager _bodyManager;
    private Dictionary<ulong, GameObject> userHats = new Dictionary<ulong, GameObject>(); // Tracks hats per user

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || hatPrefabs == null || hatPrefabs.Length == 0)
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

                // If user doesn't have a hat anchor, create one
                if (!userHats.ContainsKey(userId))
                {
                    GameObject hatAnchor = new GameObject("HatAnchor_" + userId);

                    //  Randomly select a hat
                    GameObject selectedHat = hatPrefabs[Random.Range(0, hatPrefabs.Length)];
                    GameObject hatModel = Instantiate(selectedHat, hatAnchor.transform);
                    hatModel.name = "HatModel";

                    userHats[userId] = hatAnchor;
                }

                // Attach hat to the Head joint
                GameObject skeleton = GameObject.Find("Body:" + userId);
                if (skeleton != null)
                {
                    Transform headTransform = skeleton.transform.Find("Head");
                    if (headTransform != null)
                    {
                        GameObject hatAnchor = userHats[userId];
                        Transform hatModel = hatAnchor.transform.Find("HatModel");

                        //  Dynamic scaling based on torso height
                        float scaleMultiplier = GetScaleMultiplier(body);
                        float finalScale = scaleMultiplier * hatScaleOffset;

                        // Parent anchor to head
                        hatAnchor.transform.SetParent(headTransform);

                        // Position and orientation
                        hatAnchor.transform.localPosition = hatPositionOffset;

                        Kinect.Vector4 orientation = body.JointOrientations[Kinect.JointType.Head].Orientation;
                        Quaternion jointRotation = new Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
                        Quaternion manualOffset = Quaternion.Euler(0f, hatRotationY, 0f);
                        hatAnchor.transform.localRotation = jointRotation * manualOffset;

                        // Scale model only
                        if (hatModel != null)
                        {
                            hatModel.localScale = Vector3.one * finalScale;
                        }
                    }
                }
            }
        }

        // Remove hats from users who left
        List<ulong> usersToRemove = new List<ulong>();
        foreach (ulong userId in userHats.Keys)
        {
            if (!activeUserIds.Contains(userId))
                usersToRemove.Add(userId);
        }

        foreach (ulong userId in usersToRemove)
        {
            Destroy(userHats[userId]);
            userHats.Remove(userId);
        }
    }

    //  Eased torso-based scale (same as vest)
    private float GetScaleMultiplier(Kinect.Body body)
    {
        Kinect.Joint neck = body.Joints[Kinect.JointType.Neck];
        Kinect.Joint spineBase = body.Joints[Kinect.JointType.SpineBase];

        float torsoHeight = Mathf.Abs(neck.Position.Y - spineBase.Position.Y);
        float baseTorsoHeight = 0.5f; // Average adult torso height in meters

        float rawScale = torsoHeight / baseTorsoHeight;
        float easedScale = Mathf.Pow(rawScale, 0.5f); // square root easing
        return Mathf.Clamp(easedScale, 0.7f, 1.3f);
    }
}
