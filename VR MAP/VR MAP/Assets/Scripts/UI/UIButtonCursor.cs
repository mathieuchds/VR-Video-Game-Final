using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Selectable))]
public class UIButtonCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Optionnel → si vide, utilise CursorManager.defaultHoverCursor")]
    public Texture2D hoverCursor;
    public Vector2 hoverHotspot = Vector2.zero;

    [Header("Animation prefab (optionnel)")]
    [Tooltip("Si true, active l'animation du prefab via CursorPrefabManager lors du hover")]
    public bool usePrefabAnimation = true;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetHoverCursor(hoverCursor, hoverHotspot);

        if (usePrefabAnimation && CursorPrefabManager.Instance != null)
            CursorPrefabManager.Instance.PlayHoverAnimation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.ResetToDefault();

        if (usePrefabAnimation && CursorPrefabManager.Instance != null)
            CursorPrefabManager.Instance.StopHoverAnimation();
    }
}
