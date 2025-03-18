using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class ClothingTracker : MonoBehaviour
{
    public GameObject bodySourceManager;  // Kinect body tracking manager
    public GameObject rgbPlane;           // The plane displaying the RGB feed
    public GameObject hatPrefab;          // Hat model
    public GameObject uniformPrefab;      // Uniform model

    private BodySourceManager _bodyManager;
    private Dictionary<ulong, GameObject> _TrackedUsers = new Dictionary<ulong, GameObject>();

    void Start()
    {
        if (bodySourceManager != null)
        {
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
        }
        else
        {
            Debug.LogError("[ClothingTracker] ERROR: BodySourceManager is NULL!");
        }
    }

    void Update()
    {
        if (_bodyManager == null || rgbPlane == null)
        {
            return;
        }

        Kinect.Body[] data = _bodyManager.GetData();
        if (data == null)
        {
            return;
        }

        List<ulong> trackedIds = new List<ulong>();

        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                trackedIds.Add(body.TrackingId);
                if (!_TrackedUsers.ContainsKey(body.TrackingId))
                {
                    _TrackedUsers[body.TrackingId] = CreateClothingObjects(body.TrackingId);
                }
                UpdateClothing(body, _TrackedUsers[body.TrackingId]);
            }
        }

        // Remove users who are no longer detected
        List<ulong> knownIds = new List<ulong>(_TrackedUsers.Keys);
        foreach (ulong trackingId in knownIds)
        {
            if (!trackedIds.Contains(trackingId))
            {
                Destroy(_TrackedUsers[trackingId]);
                _TrackedUsers.Remove(trackingId);
            }
        }
    }

    private GameObject CreateClothingObjects(ulong id)
    {
        GameObject userObject = new GameObject("User_" + id);
        userObject.transform.SetParent(rgbPlane.transform, true);  // Attach to the RGB plane

        // Attach hat
        GameObject hat = Instantiate(hatPrefab, userObject.transform);
        hat.name = "Hat";

        // Attach uniform
        GameObject uniform = Instantiate(uniformPrefab, userObject.transform);
        uniform.name = "Uniform";

        return userObject;
    }

    private void UpdateClothing(Kinect.Body body, GameObject userObject)
    {
        Transform hatTransform = userObject.transform.Find("Hat");
        Transform uniformTransform = userObject.transform.Find("Uniform");

        if (hatTransform != null)
        {
            Vector3 headPos = GetVector3FromJoint(body.Joints[Kinect.JointType.Head]);
            hatTransform.localPosition = ConvertToWorldPosition(headPos);
        }

        if (uniformTransform != null)
        {
            Vector3 spinePos = GetVector3FromJoint(body.Joints[Kinect.JointType.SpineMid]);
            uniformTransform.localPosition = ConvertToWorldPosition(spinePos);
        }

        // Scale based on body size
        float scaleFactor = GetScaleMultiplier(body);
        userObject.transform.localScale = Vector3.one * scaleFactor;
    }

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);
    }

    private Vector3 ConvertToWorldPosition(Vector3 jointPosition)
    {
        // Convert to local position on the RGB plane
        Bounds planeBounds = rgbPlane.GetComponent<Renderer>().bounds;

        float worldX = Mathf.Lerp(planeBounds.min.x, planeBounds.max.x, (jointPosition.x + 1) / 2);
        float worldY = Mathf.Lerp(planeBounds.min.y, planeBounds.max.y, (jointPosition.y + 1) / 2);
        float worldZ = planeBounds.center.z;

        return new Vector3(worldX, worldY, worldZ);
    }

    private float GetScaleMultiplier(Kinect.Body body)
    {
        Kinect.Joint leftShoulder = body.Joints[Kinect.JointType.ShoulderLeft];
        Kinect.Joint rightShoulder = body.Joints[Kinect.JointType.ShoulderRight];

        float shoulderWidth = Mathf.Abs(leftShoulder.Position.X - rightShoulder.Position.X);
        float baseSize = 0.3f;
        return Mathf.Clamp(baseSize + (shoulderWidth * 2.5f), 0.5f, 3f);
    }
}
