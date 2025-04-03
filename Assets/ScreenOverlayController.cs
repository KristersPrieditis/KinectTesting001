using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenOverlayController : MonoBehaviour
{
    public CanvasGroup blackOverlayCanvas;    // Full black screen panel
    public CanvasGroup messageCanvas;         // Contains message + QR
    public float fadeToBlackDuration = 2f;
    public float delayBeforeMessage = 1f;
    public float displayDuration = 6f;

    private ImageFadeInAfterSkeleton fadeInScript;

    void Start()
    {
        blackOverlayCanvas.alpha = 0f;
        messageCanvas.alpha = 0f;
        fadeInScript = FindObjectOfType<ImageFadeInAfterSkeleton>();

        StartCoroutine(WatchForTrigger());
    }

    IEnumerator WatchForTrigger()
    {
        while (true)
        {
            if (fadeInScript != null && fadeInScript.HasFadedIn)
            {
                yield return new WaitForSeconds(10f); //  Add pause after image fade-in
                yield return StartCoroutine(PlayOverlaySequence());
            }
            yield return null;
        }
    }

    IEnumerator PlayOverlaySequence()
    {
        // Fade screen to black
        float t = 0f;
        while (t < fadeToBlackDuration)
        {
            t += Time.deltaTime;
            blackOverlayCanvas.alpha = Mathf.Lerp(0f, 1f, t / fadeToBlackDuration);
            yield return null;
        }

        blackOverlayCanvas.alpha = 1f;
        yield return new WaitForSeconds(delayBeforeMessage);

        // Fade in message
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            messageCanvas.alpha = Mathf.Lerp(0f, 1f, t / 1f);
            yield return null;
        }

        messageCanvas.alpha = 1f;
        yield return new WaitForSeconds(displayDuration);

        // Fade everything out
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            messageCanvas.alpha = Mathf.Lerp(1f, 0f, t / 1f);
            blackOverlayCanvas.alpha = Mathf.Lerp(1f, 0f, t / 1f);
            yield return null;
        }

        messageCanvas.alpha = 0f;
        blackOverlayCanvas.alpha = 0f;

        fadeInScript.ResetSequence();
    }
}
