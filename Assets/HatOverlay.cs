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
    private ulong trackedUserId = 0;

    private float depthOffset = 0.3f;  // Keep hat slightly in front of the plane
    private float baseHatScale = 30.0f; // Adjusted base scale for visibility
    private float verticalHatOffset = 0.0001f; // Force height to sit directly on head
    private float smoothFactor = 0.15f;  // **Smooth movement & rotation**

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || hatPrefab == null || rgbPlane == null)
            return;

        Kinect.Body[] data = _bodyManager.GetData();
        if (data == null)
            return;

        bool userDetected = false;

        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                trackedUserId = body.TrackingId;
                userDetected = true;

                // If the hat hasn't been spawned, create it
                if (spawnedHat == null)
                {
                    spawnedHat = Instantiate(hatPrefab);
                    spawnedHat.transform.parent = null; // **Ensure it's not parented to the plane**
                }

                // Get head and neck positions
                Kinect.Joint headJoint = body.Joints[Kinect.JointType.Head];
                Kinect.Joint neckJoint = body.Joints[Kinect.JointType.Neck];

                Vector3 headPosition = GetVector3FromJoint(headJoint);
                Vector3 neckPosition = GetVector3FromJoint(neckJoint);

                // **Force hat to be exactly on head height**
                headPosition.y = neckPosition.y + verticalHatOffset;

                // Convert head position to world coordinates
                Vector2 uvCoords = ConvertToUVCoordinates(headPosition);
                Vector3 targetPosition = ConvertUVToWorld(uvCoords, headPosition.z);

                // **Ensure hat stays directly above head**
                targetPosition.y = headPosition.y;

                // Keep hat slightly in front of the plane
                targetPosition.z = rgbPlane.transform.position.z - depthOffset;

                // **Smooth position transition to reduce jitter**
                if (spawnedHat != null)
                {
                    spawnedHat.transform.position = Vector3.Lerp(
                        spawnedHat.transform.position, targetPosition, smoothFactor);
                }

                // Scale dynamically based on user size
                float scaleMultiplier = GetScaleMultiplier(body);
                spawnedHat.transform.localScale = Vector3.one * scaleMultiplier;

                // **Fix Rotation - Ensure hat follows head properly, flipped correctly**
                Quaternion targetRotation = GetFixedHeadRotation(neckPosition, headPosition);
                spawnedHat.transform.rotation = Quaternion.Slerp(
                    spawnedHat.transform.rotation, targetRotation, smoothFactor);

                break; // Only track the first detected user
            }
        }

        // If no user is detected, remove the hat
        if (!userDetected && spawnedHat != null)
        {
            Destroy(spawnedHat);
            spawnedHat = null;
            trackedUserId = 0;
        }
    }

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);
    }

    private Vector2 ConvertToUVCoordinates(Vector3 jointPosition)
    {
        return new Vector2((jointPosition.x + 1) / 2, (jointPosition.y + 1) / 2);
    }

    private Vector3 ConvertUVToWorld(Vector2 uv, float depth)
    {
        Bounds planeBounds = rgbPlane.GetComponent<Renderer>().bounds;

        float worldX = Mathf.Lerp(planeBounds.min.x, planeBounds.max.x, uv.x);
        float worldY = planeBounds.center.y;
        float worldZ = planeBounds.center.z - depthOffset;

        return new Vector3(worldX, worldY, worldZ);
    }

    private float GetScaleMultiplier(Kinect.Body body)
    {
        // Get the shoulder width
        Kinect.Joint leftShoulder = body.Joints[Kinect.JointType.ShoulderLeft];
        Kinect.Joint rightShoulder = body.Joints[Kinect.JointType.ShoulderRight];

        float shoulderWidth = Mathf.Abs(leftShoulder.Position.X - rightShoulder.Position.X);
        float headDepth = body.Joints[Kinect.JointType.Head].Position.Z;
        float depthFactor = 2.0f - Mathf.Clamp(headDepth, 0.5f, 2.5f);

        float scaleFactor = baseHatScale + (shoulderWidth * 2.5f) * depthFactor;
        return Mathf.Clamp(scaleFactor, 1.5f, 100f);
    }

    private Quaternion GetFixedHeadRotation(Vector3 neckPos, Vector3 headPos)
    {
        // **Fix the rotation to prevent flipping**
        Vector3 forwardDirection = headPos - neckPos;
        forwardDirection.y = 0; // **Ignore vertical movement to lock rotation**
        forwardDirection.Normalize();

        // **Apply Quaternion.Inverse to correct flip**
        Quaternion targetRotation = Quaternion.Inverse(Quaternion.LookRotation(forwardDirection)) * Quaternion.Euler(0, 180, 0);

        return targetRotation;
    }
}