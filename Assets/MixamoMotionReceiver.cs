using UnityEngine;

public class MixamoMotionReceiver : MonoBehaviour
{
    private Vector3 basePosition;

    public float smoothingSpeed = 5f;

    void Start()
    {
        basePosition = transform.position;
    }

    public void ApplyDelta(Vector3 delta, float scale = 1f)
    {
        Vector3 target = basePosition + delta * scale;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * smoothingSpeed);
    }

    public void ApplyAbsolutePosition(Vector3 worldPosition, float scale = 1f)
    {
        Vector3 target = worldPosition * scale;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * smoothingSpeed);
    }

    public void ResetBasePosition()
    {
        basePosition = transform.position;
    }
}
