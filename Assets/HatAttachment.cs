using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class HatAttachment : MonoBehaviour
{
    public GameObject bodySourceManager;  // Kinect body tracking manager
    public GameObject hatPrefab;          // The 3D hat prefab to spawn

    private BodySourceManager _bodyManager;
    private Dictionary<ulong, GameObject> userHats = new Dictionary<ulong, GameObject>(); // Tracks hats per user

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || hatPrefab == null)
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

                // If user doesn't have a hat, create one
                if (!userHats.ContainsKey(userId))
                {
                    GameObject newHat = Instantiate(hatPrefab);
                    userHats[userId] = newHat;
                }

                // Attach hat to the Head joint
                Kinect.Joint headJoint = body.Joints[Kinect.JointType.Head];

                // Find the skeleton GameObject
                GameObject skeleton = GameObject.Find("Body:" + userId);
                if (skeleton != null)
                {
                    Transform headTransform = skeleton.transform.Find("Head");
                    if (headTransform != null)
                    {
                        GameObject hat = userHats[userId];

                        // **Scale dynamically based on the user’s body size BEFORE attaching**
                        float scaleMultiplier = GetScaleMultiplier(body);
                        hat.transform.localScale = Vector3.one * scaleMultiplier;

                        // **Attach to head joint & adjust position**
                        hat.transform.SetParent(headTransform);
                        hat.transform.localPosition = Vector3.up * (scaleMultiplier * -2f); // Adjust height based on scale
                        hat.transform.localRotation = Quaternion.identity;
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

    private float GetScaleMultiplier(Kinect.Body body)
    {
        // Get the positions of the shoulders
        Kinect.Joint leftShoulder = body.Joints[Kinect.JointType.ShoulderLeft];
        Kinect.Joint rightShoulder = body.Joints[Kinect.JointType.ShoulderRight];
        Kinect.Joint head = body.Joints[Kinect.JointType.Head];

        // **Calculate shoulder width**
        float shoulderWidth = Mathf.Abs(leftShoulder.Position.X - rightShoulder.Position.X);

        // **Get the depth (distance from the camera)**
        float userDepth = head.Position.Z; // Z = distance from the Kinect

        // **Adjust the hat scale dynamically**
        float baseSize = 1.5f; // Base size of the hat
        float depthFactor = Mathf.Clamp(2.0f - userDepth, 0.5f, 2.5f); // Scale based on distance
        float scaleFactor = (shoulderWidth * 150f) * depthFactor;

        return Mathf.Clamp(scaleFactor, 10f, 150f); // Keep within reasonable limits
    }
}
