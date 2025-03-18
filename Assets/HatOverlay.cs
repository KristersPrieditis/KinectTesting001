using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Kinect = Windows.Kinect;
public class HatOverlay : MonoBehaviour
{
    public GameObject bodySourceManager;  // Kinect body tracking manager
    public GameObject hatPrefab;          // The 3D hat prefab to spawn
    public GameObject rgbPlane;           // The plane showing the RGB feed
    private BodySourceManager _bodyManager;
    private GameObject spawnedHat = null;

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

        bool userDetected = false;

        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                userDetected = true;

                if (spawnedHat == null)
                {
                    spawnedHat = Instantiate(hatPrefab);
                    spawnedHat.layer = LayerMask.NameToLayer("Clothing"); // Ensure it's visible
                }

                // Get head position in 3D world space
                Vector3 headPosition = GetVector3FromJoint(body.Joints[Kinect.JointType.Head]);

                // Convert to world position using depth data
                Vector3 worldPosition = ConvertDepthToWorld(headPosition);
                spawnedHat.transform.position = worldPosition;

                // Ensure the hat is rotated correctly
                spawnedHat.transform.rotation = Quaternion.Euler(0, 180, 0);

                // Scale hat based on user size
                float scaleMultiplier = GetScaleMultiplier(body);
                spawnedHat.transform.localScale = Vector3.one * scaleMultiplier;

                break; // Only track the first detected user
            }
        }

        if (!userDetected && spawnedHat != null)
        {
            Destroy(spawnedHat);
            spawnedHat = null;
        }
    }

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);
    }

    private Vector3 ConvertDepthToWorld(Vector3 jointPosition)
    {
        Camera mainCam = Camera.main;
        Bounds planeBounds = rgbPlane.GetComponent<Renderer>().bounds;

        Vector3 screenPoint = mainCam.WorldToScreenPoint(jointPosition);
        Vector3 worldPosition = mainCam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, jointPosition.z));

        return worldPosition;
    }

    private float GetScaleMultiplier(Kinect.Body body)
    {
        // Get shoulder positions
        Kinect.Joint leftShoulder = body.Joints[Kinect.JointType.ShoulderLeft];
        Kinect.Joint rightShoulder = body.Joints[Kinect.JointType.ShoulderRight];

        // Compute shoulder width (X distance in Kinect space)
        float shoulderWidth = Mathf.Abs(leftShoulder.Position.X - rightShoulder.Position.X);

        // Use head depth to adjust scale
        float headDepth = body.Joints[Kinect.JointType.Head].Position.Z;
        float baseSize = 0.3f;
        float depthFactor = 2.0f - Mathf.Clamp(headDepth, 0.5f, 2.5f); // Smaller if closer, larger if farther

        // Combine depth & shoulder width to get a realistic scale
        float scaleFactor = baseSize + (shoulderWidth * 2.5f) * depthFactor;

        return Mathf.Clamp(scaleFactor, 0.5f, 3f); // Prevent extreme sizes
    }
}
