using UnityEngine;

// Follows the player horizontally inside the current map. Maps narrower than the view stay centered.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public float minX = -10f, maxX = 10f;
    public Transform target;
    public float smoothing = 8f;

    Camera cam;

    void Awake() => cam = GetComponent<Camera>();

    public void SetBounds(float min, float max, bool snap)
    {
        minX = min;
        maxX = max;
        if (snap) SnapNow();
    }

    public void SnapNow()
    {
        if (cam == null) cam = GetComponent<Camera>();
        var p = transform.position;
        p.x = TargetX();
        transform.position = p;
    }

    float TargetX()
    {
        if (cam == null) cam = GetComponent<Camera>();
        float half = cam.orthographicSize * cam.aspect;
        float lo = minX + half, hi = maxX - half;
        if (lo > hi) return (minX + maxX) * 0.5f;
        float x = target != null ? target.position.x : transform.position.x;
        return Mathf.Clamp(x, lo, hi);
    }

    float shakeAmp, shakeUntil, shakeLen = 1f;
    Vector3 shakeOffset;

    // A new shake only replaces the current one when it is stronger, so many small hits do not cancel a big one.
    public void Shake(float amplitude, float duration)
    {
        if (amplitude < shakeAmp * Mathf.Clamp01((shakeUntil - Time.unscaledTime) / shakeLen)) return;
        shakeAmp = amplitude;
        shakeLen = Mathf.Max(0.01f, duration);
        shakeUntil = Time.unscaledTime + duration;
    }

    void LateUpdate()
    {
        var p = transform.position - shakeOffset;
        p.x = Mathf.Lerp(p.x, TargetX(), 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));

        float left = Mathf.Clamp01((shakeUntil - Time.unscaledTime) / shakeLen);
        shakeOffset = left > 0f ? (Vector3)(Random.insideUnitCircle * shakeAmp * left) : Vector3.zero;
        transform.position = p + shakeOffset;
    }
}
