using UnityEngine;

public class KinectJointTracker : MonoBehaviour
{
    public Vector3 InitialWorldPosition { get; private set; }
    public Vector3 InitialLocalPosition { get; private set; }

    public Vector3 WorldDelta { get; private set; }
    public Vector3 LocalDelta { get; private set; }

    private bool initialized = false;

    void Update()
    {
        if (!initialized)
        {
            InitialWorldPosition = transform.position;
            InitialLocalPosition = transform.localPosition;
            initialized = true;
        }

        WorldDelta = transform.position - InitialWorldPosition;
        LocalDelta = transform.localPosition - InitialLocalPosition;

        if (name == "ElbowRight") // or any limb you're testing
            Debug.Log($"LocalDelta {name}: {LocalDelta}");
    }
}
