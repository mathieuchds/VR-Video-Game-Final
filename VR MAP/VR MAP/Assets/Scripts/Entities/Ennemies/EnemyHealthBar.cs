using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Transform bar;

    public void SetHealth(float ratio)
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (bar == null)
        {
            bar = transform;
        }
        ratio = Mathf.Clamp01(ratio);

        bar.localScale = new Vector3(ratio, 0.2f, 1f);

        var renderer = bar.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Assurer qu'on modifie une instance de material (évite d'altérer le sharedMaterial global)
            var mat = renderer.material;

            // Si possible, utiliser un shader non-éclairé pour éviter que la barre devienne noire à cause de l'éclairage
            var unlit = Shader.Find("Unlit/Color");
            if (unlit != null && mat != null && mat.shader != unlit)
            {
                mat.shader = unlit;
            }

            if (mat != null)
            {
                mat.color = Color.Lerp(Color.red, Color.green, ratio);
            }
        }
    }

    void LateUpdate()
    {
        if (cam != null)
        {
            transform.LookAt(cam.transform);
            transform.Rotate(0, 180f, 0);
        }
    }
}
