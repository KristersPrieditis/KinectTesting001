using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class VestAttachment : MonoBehaviour
{
    public GameObject bodySourceManager;  // Kinect body tracking manager
    public GameObject vestPrefab;         // The 3D vest prefab to spawn
    public float vestScaleOffset = 1f;    // Manual scaling factor for fine-tuning size

    private BodySourceManager _bodyManager;
    private Dictionary<ulong, GameObject> userVests = new Dictionary<ulong, GameObject>(); // Tracks vests per user

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || vestPrefab == null)
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

                // If user doesn't have a vest, create one
                if (!userVests.ContainsKey(userId))
                {
                    GameObject newVest = Instantiate(vestPrefab);
                    userVests[userId] = newVest;
                }

                // Attach vest to the SpineShoulder joint (chest area)
                Kinect.Joint chestJoint = body.Joints[Kinect.JointType.SpineShoulder];

                // Find the skeleton GameObject
                GameObject skeleton = GameObject.Find("Body:" + userId);
                if (skeleton != null)
                {
                    Transform chestTransform = skeleton.transform.Find("SpineShoulder");
                    if (chestTransform != null)
                    {
                        GameObject vest = userVests[userId];

                        // Dynamic scaling based on user size
                        float scaleMultiplier = GetScaleMultiplier(body);

                        // Apply extra public scale offset
                        float finalScale = scaleMultiplier * vestScaleOffset;
                        vest.transform.localScale = Vector3.one * finalScale;

                        // Attach to chest joint
                        vest.transform.SetParent(chestTransform);

                        // Position the vest slightly forward/down depending on fit
                        vest.transform.localPosition = new Vector3(0f, -finalScale * 0.1f, 0f);

                        // Apply default rotation facing forward
                        vest.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
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
        Kinect.Joint leftShoulder = body.Joints[Kinect.JointType.ShoulderLeft];
        Kinect.Joint rightShoulder = body.Joints[Kinect.JointType.ShoulderRight];
        Kinect.Joint spine = body.Joints[Kinect.JointType.SpineMid];

        float shoulderWidth = Mathf.Abs(leftShoulder.Position.X - rightShoulder.Position.X);
        float userDepth = spine.Position.Z;

        float depthFactor = Mathf.Clamp(2.0f - userDepth, 0.5f, 2.5f);
        float scaleFactor = (shoulderWidth * 200f) * depthFactor;

        return Mathf.Clamp(scaleFactor, 10f, 150f);
    }
}
