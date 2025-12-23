using UnityEngine;

public class ShockwaveVFX : MonoBehaviour
{
    public float duration = 0.15f;

    public void Play(float radius)
    {
        StartCoroutine(Animate(radius));
    }

    private System.Collections.IEnumerator Animate(float radius)
    {
        float t = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * radius * 2f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;

            transform.localScale = Vector3.Lerp(startScale, endScale, k);
            yield return null;
        }

        Destroy(gameObject);
    }
}
