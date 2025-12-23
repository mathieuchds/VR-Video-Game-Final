using UnityEngine;
using UnityEngine.UI;

public class PopUp : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("GameObject racine du pop-up (Panel). Doit être désactivé par défaut si vous ne voulez pas l'afficher au start).")]
    public GameObject popupRoot;

    [Tooltip("ScrollRect contenant la zone de texte (vertical).")]
    public ScrollRect scrollRect;

    [Tooltip("Composant Text qui affiche le contenu (règles).")]
    public Text contentText;

    [Header("Options")]
    [Tooltip("Si true, on centre automatiquement la popup dans son parent Canvas au Show")]
    public bool centerOnShow = true;

    [Header("Placement au-dessus des autres UI")]
    [Tooltip("Si non assigné, on recherche le Canvas parent automatiquement.")]
    public Canvas parentCanvas;

    [Tooltip("Si true, déplace le popup comme enfant direct du Canvas (évite que des LayoutGroups parents repositionnent).")]
    public bool moveToRootCanvas = true;

    [Tooltip("Si true, ajoute/active un Canvas sur le popup et force le tri (overrideSorting) pour le placer au-dessus.")]
    public bool forceOverrideCanvas = true;

    [Tooltip("Ordre de tri si forceOverrideCanvas = true (plus grand = au-dessus).")]
    public int popupSortingOrder = 100;

    [Header("Close button")]
    [Tooltip("Bouton de fermeture (peut être enfant du popup ou ailleurs). Sera forcé au-dessus du popup si renseigné.")]
    public GameObject closeButton;

    [Tooltip("Offset de sortingOrder appliqué au Canvas du closeButton par rapport à popupSortingOrder.")]
    public int closeButtonSortingOffset = 1;

    void Awake()
    {
        if (popupRoot == null)
            popupRoot = gameObject;

        // S'assurer que la popup est cachée au démarrage si vous voulez ce comportement.
        // Si vous préférez visible au start, désactivez cette ligne et activez le GameObject dans l'éditeur.
        if (popupRoot != null && popupRoot.activeSelf)
            popupRoot.SetActive(false);

        // Si aucun Canvas assigné, tenter de trouver un Canvas parent
        if (parentCanvas == null && popupRoot != null)
            parentCanvas = popupRoot.GetComponentInParent<Canvas>();
    }

    /// <summary>
    /// Méthode pour assigner au bouton OnClick : toggle (affiche/masque).
    /// </summary>
    public void OnToggleButtonClicked()
    {
        Toggle();
    }

    /// <summary>
    /// Basculer l'état visible / caché.
    /// </summary>
    public void Toggle()
    {
        if (IsVisible())
            Hide();
        else
            Show();
    }

    /// <summary>
    /// Affiche la popup. Si content != null, remplace le texte avant d'afficher.
    /// </summary>
    public void Show(string content = null)
    {
        if (popupRoot == null) return;

        if (!string.IsNullOrEmpty(content) && contentText != null)
            contentText.text = content;

        // Si demandé, s'assurer que le popup est enfant direct du Canvas (évite LayoutGroups qui repositionnent)
        if (moveToRootCanvas)
        {
            if (parentCanvas == null)
                parentCanvas = popupRoot.GetComponentInParent<Canvas>();

            if (parentCanvas != null && popupRoot.transform.parent != parentCanvas.transform)
            {
                popupRoot.transform.SetParent(parentCanvas.transform, false);
            }
        }

        // Activer d'abord pour que les Layout/Rect soient calculés
        popupRoot.SetActive(true);

        // Forcer mise à jour des layouts afin que la taille réelle soit disponible avant le centrage
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            // S'assurer que le ScrollRect est activé
            if (!scrollRect.enabled) scrollRect.enabled = true;

            // Si un scrollbar vertical existe, forcer son affichage et son interactivité
            if (scrollRect.verticalScrollbar != null)
            {
                scrollRect.verticalScrollbar.gameObject.SetActive(true);
                scrollRect.verticalScrollbar.interactable = true;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            // Rebuild du contenu et reset du scroll en haut
            if (scrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            }

            scrollRect.velocity = Vector2.zero;
            // Forcer une nouvelle passe UI puis positionner en haut
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // Si besoin, ajouter/activer un Canvas sur le popup pour forcer rendu au-dessus
        if (forceOverrideCanvas)
        {
            var c = popupRoot.GetComponent<Canvas>();
            if (c == null)
                c = popupRoot.AddComponent<Canvas>();

            c.overrideSorting = true;
            c.sortingOrder = popupSortingOrder;

            // Optionnel : s'assurer qu'il y a un GraphicRaycaster si interaction nécessaire
            if (popupRoot.GetComponent<GraphicRaycaster>() == null)
                popupRoot.AddComponent<GraphicRaycaster>();
        }

        // Si un closeButton est fourni, lui ajouter/activer un Canvas avec sorting supérieur
        if (closeButton != null)
        {
            var closeCanvas = closeButton.GetComponent<Canvas>();
            if (closeCanvas == null)
                closeCanvas = closeButton.AddComponent<Canvas>();

            closeCanvas.overrideSorting = true;
            closeCanvas.sortingOrder = popupSortingOrder + closeButtonSortingOffset;

            // s'assurer que le bouton est visible au-dessus (optionnel: ajouter GraphicRaycaster si nécessaire)
            if (closeButton.GetComponent<GraphicRaycaster>() == null)
                closeButton.AddComponent<GraphicRaycaster>();
        }

        if (centerOnShow)
            CenterPopup();

        // s'assurer que le popup est au-dessus dans la hiérarchie UI
        var rt = popupRoot.GetComponent<RectTransform>();
        if (rt != null)
            rt.SetAsLastSibling();
        else
            popupRoot.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Masque la popup.
    /// </summary>
    public void Hide()
    {
        if (popupRoot == null) return;
        popupRoot.SetActive(false);
    }

    /// <summary>
    /// Définit le texte du contenu sans ouvrir la popup.
    /// </summary>
    public void SetContent(string content)
    {
        if (contentText != null)
            contentText.text = content;
    }

    /// <summary>
    /// Renvoie si la popup est affichée.
    /// </summary>
    public bool IsVisible()
    {
        return popupRoot != null && popupRoot.activeSelf;
    }

    private void CenterPopup()
    {
        if (popupRoot == null) return;
        var rt = popupRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        // Forcer anchors/pivot au centre et remettre la position à (0,0)
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Réinitialiser la taille locale si des échelles bizarres existent
        rt.localScale = Vector3.one;

        // Définir l'anchoredPosition à zero pour centrer dans le parent Canvas
        rt.anchoredPosition = Vector2.zero;

        // Aussi forcer la position locale à (0,0,0) — utile si worldPosition changed
        rt.localPosition = Vector3.zero;
    }
}
