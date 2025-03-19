using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class BodySourceKristers : MonoBehaviour
{
    public Material BoneMaterial;
    public GameObject BodySourceManager;
    public GameObject rgbPlane; // The plane showing the RGB feed

    [Header("Skeleton Scaling")]
    public float skeletonScaleFactor = 1.0f; // Adjustable in Inspector

    private Dictionary<ulong, GameObject> _Bodies = new Dictionary<ulong, GameObject>();
    private BodySourceManager _BodyManager;

    private Dictionary<Kinect.JointType, Kinect.JointType> _BoneMap = new Dictionary<Kinect.JointType, Kinect.JointType>()
    {
        { Kinect.JointType.FootLeft, Kinect.JointType.AnkleLeft },
        { Kinect.JointType.AnkleLeft, Kinect.JointType.KneeLeft },
        { Kinect.JointType.KneeLeft, Kinect.JointType.HipLeft },
        { Kinect.JointType.HipLeft, Kinect.JointType.SpineBase },

        { Kinect.JointType.FootRight, Kinect.JointType.AnkleRight },
        { Kinect.JointType.AnkleRight, Kinect.JointType.KneeRight },
        { Kinect.JointType.KneeRight, Kinect.JointType.HipRight },
        { Kinect.JointType.HipRight, Kinect.JointType.SpineBase },

        { Kinect.JointType.HandTipLeft, Kinect.JointType.HandLeft },
        { Kinect.JointType.ThumbLeft, Kinect.JointType.HandLeft },
        { Kinect.JointType.HandLeft, Kinect.JointType.WristLeft },
        { Kinect.JointType.WristLeft, Kinect.JointType.ElbowLeft },
        { Kinect.JointType.ElbowLeft, Kinect.JointType.ShoulderLeft },
        { Kinect.JointType.ShoulderLeft, Kinect.JointType.SpineShoulder },

        { Kinect.JointType.HandTipRight, Kinect.JointType.HandRight },
        { Kinect.JointType.ThumbRight, Kinect.JointType.HandRight },
        { Kinect.JointType.HandRight, Kinect.JointType.WristRight },
        { Kinect.JointType.WristRight, Kinect.JointType.ElbowRight },
        { Kinect.JointType.ElbowRight, Kinect.JointType.ShoulderRight },
        { Kinect.JointType.ShoulderRight, Kinect.JointType.SpineShoulder },

        { Kinect.JointType.SpineBase, Kinect.JointType.SpineMid },
        { Kinect.JointType.SpineMid, Kinect.JointType.SpineShoulder },
        { Kinect.JointType.SpineShoulder, Kinect.JointType.Neck },
        { Kinect.JointType.Neck, Kinect.JointType.Head },
    };

    void Update()
    {
        if (BodySourceManager == null || rgbPlane == null)
        {
            return;
        }

        _BodyManager = BodySourceManager.GetComponent<BodySourceManager>();
        if (_BodyManager == null)
        {
            return;
        }

        Kinect.Body[] data = _BodyManager.GetData();
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
            }
        }

        List<ulong> knownIds = new List<ulong>(_Bodies.Keys);

        // Delete untracked bodies
        foreach (ulong trackingId in knownIds)
        {
            if (!trackedIds.Contains(trackingId))
            {
                Destroy(_Bodies[trackingId]);
                _Bodies.Remove(trackingId);
            }
        }

        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                if (!_Bodies.ContainsKey(body.TrackingId))
                {
                    _Bodies[body.TrackingId] = CreateBodyObject(body.TrackingId);
                }

                RefreshBodyObject(body, _Bodies[body.TrackingId]);
            }
        }
    }

    private GameObject CreateBodyObject(ulong id)
    {
        GameObject body = new GameObject("Body:" + id);
        body.transform.SetParent(rgbPlane.transform, true); // Attach skeleton to RGB plane

        // Apply initial scaling to the entire skeleton
        body.transform.localScale = Vector3.one * skeletonScaleFactor;

        foreach (Kinect.JointType jt in System.Enum.GetValues(typeof(Kinect.JointType)))
        {
            GameObject jointObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            jointObj.transform.parent = body.transform;
            jointObj.transform.localScale = Vector3.one * 0.1f * skeletonScaleFactor; // Scale joints

            LineRenderer lr = jointObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.material = BoneMaterial;
            lr.startWidth = 0.02f * skeletonScaleFactor;
            lr.endWidth = 0.02f * skeletonScaleFactor;
        }

        return body;
    }

    private void RefreshBodyObject(Kinect.Body body, GameObject skeleton)
    {
        foreach (Kinect.JointType jt in System.Enum.GetValues(typeof(Kinect.JointType)))
        {
            Kinect.Joint sourceJoint = body.Joints[jt];
            Transform jointObj = skeleton.transform.Find(jt.ToString());

            if (jointObj != null)
            {
                // Convert the joint to the plane surface
                Vector3 jointPos = GetVector3FromJoint(sourceJoint);
                Vector2 uvCoords = ConvertToUVCoordinates(jointPos);
                jointObj.localPosition = ConvertUVToWorld(uvCoords);
            }
        }
    }

    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X, joint.Position.Y, 0);
    }

    private Vector2 ConvertToUVCoordinates(Vector3 jointPosition)
    {
        return new Vector2((jointPosition.x + 1) / 2, (jointPosition.y + 1) / 2);
    }

    private Vector3 ConvertUVToWorld(Vector2 uv)
    {
        Bounds planeBounds = rgbPlane.GetComponent<Renderer>().bounds;

        float worldX = Mathf.Lerp(planeBounds.min.x, planeBounds.max.x, uv.x);
        float worldY = Mathf.Lerp(planeBounds.min.y, planeBounds.max.y, uv.y);
        float worldZ = planeBounds.center.z; // Keep skeleton on the plane

        return new Vector3(worldX, worldY, worldZ);
    }
}