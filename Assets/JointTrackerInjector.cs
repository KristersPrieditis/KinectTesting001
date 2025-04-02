using System.Collections.Generic;
using UnityEngine;

public class JointTrackerInjector : MonoBehaviour
{
    public List<string> jointsToTrack = new List<string>
    {
        "SpineBase",
        "SpineMid",
        "SpineShoulder",
        "ShoulderLeft",
        "ElbowLeft",
        "WristLeft",
        "ShoulderRight",
        "ElbowRight",
        "WristRight",
        "HipLeft",
        "KneeLeft",
        "AnkleLeft",
        "HipRight",
        "KneeRight",
        "AnkleRight"
    };

    void LateUpdate()
    {
        foreach (Transform child in transform)
        {
            if (jointsToTrack.Contains(child.name) && child.GetComponent<KinectJointTracker>() == null)
            {
                child.gameObject.AddComponent<KinectJointTracker>();
                Debug.Log($"Added KinectJointTracker to {child.name}");
            }
        }
    }
}
