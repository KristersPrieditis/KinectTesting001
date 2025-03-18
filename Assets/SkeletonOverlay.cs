using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

public class SkeletonOverlay : MonoBehaviour
{
    public GameObject colorSourceCube;  // The cube displaying the Kinect RGB feed
    public GameObject bodySourceManager; // Kinect body tracking manager
    public Material boneMaterial;      // Material for skeleton lines

    private BodySourceManager _bodyManager;
    private Dictionary<ulong, GameObject> _skeletons = new Dictionary<ulong, GameObject>();

    void Start()
    {
        if (bodySourceManager != null)
            _bodyManager = bodySourceManager.GetComponent<BodySourceManager>();
    }

    void Update()
    {
        if (_bodyManager == null || colorSourceCube == null)
            return;

        Kinect.Body[] data = _bodyManager.GetData();
        if (data == null)
            return;

        List<ulong> trackedIds = new List<ulong>();

        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                ulong id = body.TrackingId;
                trackedIds.Add(id);

                if (!_skeletons.ContainsKey(id))
                {
                    _skeletons[id] = CreateSkeleton(id);
                }

                UpdateSkeleton(body, _skeletons[id]);
            }
        }

        RemoveUntrackedUsers(trackedIds);
    }

    private GameObject CreateSkeleton(ulong id)
    {
        GameObject skeleton = new GameObject("Skeleton:" + id);
        skeleton.transform.SetParent(colorSourceCube.transform, true); // Attach skeleton to RGB cube

        foreach (Kinect.JointType jt in System.Enum.GetValues(typeof(Kinect.JointType)))
        {
            GameObject jointObj = new GameObject(jt.ToString());
            jointObj.transform.parent = skeleton.transform;

            LineRenderer lr = jointObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.material = boneMaterial;
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
        }

        return skeleton;
    }

    private void UpdateSkeleton(Kinect.Body body, GameObject skeleton)
    {
        foreach (Kinect.JointType jt in System.Enum.GetValues(typeof(Kinect.JointType)))
        {
            if (body.Joints.ContainsKey(jt))
            {
                Kinect.Joint joint = body.Joints[jt];
                Transform jointObj = skeleton.transform.Find(jt.ToString());

                if (jointObj != null)
                {
                    Vector3 jointPos = GetVector3FromJoint(joint);
                    Vector2 uvCoords = ConvertToUVCoordinates(jointPos);
                    jointObj.localPosition = ConvertUVToWorld(uvCoords);
                }
            }
        }
    }

    private void RemoveUntrackedUsers(List<ulong> trackedIds)
    {
        List<ulong> knownIds = new List<ulong>(_skeletons.Keys);

        foreach (ulong id in knownIds)
        {
            if (!trackedIds.Contains(id))
            {
                Destroy(_skeletons[id]);
                _skeletons.Remove(id);
            }
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

    private Vector3 ConvertUVToWorld(Vector2 uv)
    {
        Bounds cubeBounds = colorSourceCube.GetComponent<Renderer>().bounds;

        float worldX = Mathf.Lerp(cubeBounds.min.x, cubeBounds.max.x, uv.x);
        float worldY = Mathf.Lerp(cubeBounds.min.y, cubeBounds.max.y, uv.y);
        float worldZ = cubeBounds.max.z; // Adjust to the front face of the cube

        return new Vector3(worldX, worldY, worldZ);
    }
}
