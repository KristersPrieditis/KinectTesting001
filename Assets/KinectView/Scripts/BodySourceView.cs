using UnityEngine;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class BodySourceView : MonoBehaviour
{
    public Material BoneMaterial;
    public GameObject BodySourceManager;

    [Header("Skeleton Scaling")]
    public float skeletonScaleFactor = 1.0f; // Adjustable scale factor for skeleton

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
        if (BodySourceManager == null) return;
        _BodyManager = BodySourceManager.GetComponent<BodySourceManager>();
        if (_BodyManager == null) return;

        Kinect.Body[] data = _BodyManager.GetData();
        if (data == null) return;

        List<ulong> trackedIds = new List<ulong>();
        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
                trackedIds.Add(body.TrackingId);
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

                GameObject skeleton = _Bodies[body.TrackingId];

                // **Scale the whole skeleton properly**
                skeleton.transform.localScale = Vector3.one * skeletonScaleFactor;

                RefreshBodyObject(body, skeleton);
            }
        }
    }

    private GameObject CreateBodyObject(ulong id)
    {
        GameObject body = new GameObject("Body:" + id);

        foreach (Kinect.JointType jt in _BoneMap.Keys)
        {
            GameObject jointObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jointObj.transform.localScale = Vector3.one * 0.3f; // Default joint size

            LineRenderer lr = jointObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.material = BoneMaterial;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;

            jointObj.name = jt.ToString();
            jointObj.transform.parent = body.transform;
        }

        body.AddComponent<JointTrackerInjector>();

        return body;
    }

    private void RefreshBodyObject(Kinect.Body body, GameObject bodyObject)
    {
        foreach (var jt in _BoneMap.Keys)
        {
            if (!body.Joints.ContainsKey(jt)) continue;

            Kinect.Joint sourceJoint = body.Joints[jt];
            Kinect.Joint? targetJoint = null;

            if (_BoneMap.ContainsKey(jt))
                targetJoint = body.Joints[_BoneMap[jt]];

            Transform jointObj = bodyObject.transform.Find(jt.ToString());
            if (jointObj == null) continue;

            // **Apply the scale factor to joint positions**
            Vector3 scaledPosition = GetVector3FromJoint(sourceJoint) * skeletonScaleFactor;
            jointObj.localPosition = scaledPosition;

            LineRenderer lr = jointObj.GetComponent<LineRenderer>();
            if (targetJoint.HasValue)
            {
                Vector3 scaledTargetPosition = GetVector3FromJoint(targetJoint.Value) * skeletonScaleFactor;
                lr.SetPosition(0, scaledPosition);
                lr.SetPosition(1, scaledTargetPosition);
                lr.startColor = GetColorForState(sourceJoint.TrackingState);
                lr.endColor = GetColorForState(targetJoint.Value.TrackingState);
            }
            else
            {
                lr.enabled = false;
            }
        }
    }

    private static Color GetColorForState(Kinect.TrackingState state)
    {
        switch (state)
        {
            case Kinect.TrackingState.Tracked:
                return Color.green;
            case Kinect.TrackingState.Inferred:
                return Color.red;
            default:
                return Color.black;
        }
    }

    private static Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);
    }
}
