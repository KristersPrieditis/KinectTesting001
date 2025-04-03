using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Kinect = Windows.Kinect;

public class ImageFadeInAfterSkeleton : MonoBehaviour
{
    public GameObject bodySourceManager;
    public CanvasGroup imageCanvasGroup;
    public float delayBeforeFadeIn = 10f;
    public float fadeDuration = 3f;

    private BodySourceManager _bodyManager;
    private ulong currentTrackedUserId = 0;
    private float skeletonTimer = 0f;
    private bool hasFadedIn = false;
    private bool isFading = false;

    public bool HasFadedIn => hasFadedIn;

    void Start()
    {
        _bodyManager = bodySourceManager?.GetComponent<BodySourceManager>();
        if (imageCanvasGroup != null) imageCanvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (_bodyManager == null || imageCanvasGroup == null) return;

        var data = _bodyManager.GetData();
        if (data == null) return;

        bool userStillTracked = false;

        foreach (var body in data)
        {
            if (body != null && body.IsTracked)
            {
                if (currentTrackedUserId == 0)
                    currentTrackedUserId = body.TrackingId;

                if (body.TrackingId == currentTrackedUserId)
                {
                    userStillTracked = true;
                    break;
                }
            }
        }

        if (userStillTracked)
        {
            if (!hasFadedIn)
            {
                skeletonTimer += Time.deltaTime;
                if (skeletonTimer >= delayBeforeFadeIn && !isFading)
                    StartCoroutine(FadeInImage());
            }
        }
        else
        {
            ResetSequence(); // Person left early
        }
    }

    IEnumerator FadeInImage()
    {
        isFading = true;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            imageCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        imageCanvasGroup.alpha = 1f;
        hasFadedIn = true;
        isFading = false;
    }

    public void ResetSequence()
    {
        currentTrackedUserId = 0;
        skeletonTimer = 0f;
        hasFadedIn = false;
        isFading = false;
        if (imageCanvasGroup != null) imageCanvasGroup.alpha = 0f;
    }
}
