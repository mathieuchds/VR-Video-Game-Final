using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class CursorPrefabManager : MonoBehaviour
{
    public static CursorPrefabManager Instance { get; private set; }

    [Header("Prefab du curseur animé")]
    [Tooltip("Prefab contenant un RectTransform + éventuellement un Animator")]
    public GameObject cursorPrefab;

    [Tooltip("Canvas cible (optionnel). Si mis, le prefab sera instancié comme enfant du Canvas.")]
    public Canvas parentCanvas;
        
    [Tooltip("Décalage en pixels du prefab par rapport à la position de la souris")]
    public Vector2 screenOffset = Vector2.zero;

    [Tooltip("Le prefab suit la souris si true")]
    public bool followMouse = true;

    [Tooltip("Cacher le curseur système quand le prefab est visible")]
    public bool hideSystemCursorWhenShown = true;

    [Tooltip("Si true, instancie et affiche le curseur prefab automatiquement au démarrage")]
    public bool showOnStart = true;

    [Header("Canvas / tri du curseur")]
    [Tooltip("Si true, on ajoute/active un Canvas sur l'instance du curseur et force le tri.")]
    public bool forceCursorCanvas = true;

    [Tooltip("Sorting order appliqué au Canvas du curseur (doit être supérieur au popupSortingOrder).")]
    public int cursorSortingOrder = 1000;

    [Header("Remplacement complet du curseur")]
    [Tooltip("Si true, on remplace le curseur système par une texture transparente quand le prefab est visible.")]
    public bool replaceSystemCursor = true;

    private GameObject instance;
    private RectTransform instanceRect;
    private Animator instanceAnimator;

    // blank texture used to hide system cursor reliably
    private Texture2D blankCursor;
    private bool cursorReplaced = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (showOnStart)
            Show();
    }

    void Update()
    {
        if (instance == null || !instance.activeSelf || !followMouse) return;

        Vector2 mousePos = GetMousePosition();
        UpdateInstancePosition(mousePos);
    }

    // Affiche le prefab (etat d'animation PAR DEFAUT : désactivé)
    public void Show()
    {
        EnsureInstance();

        // Si le parentCanvas assigné est null ou désactivé, reparenter vers le Canvas actif le plus haut
        Canvas desired = parentCanvas;
        if (desired == null || !desired.gameObject.activeInHierarchy)
        {
            desired = FindTopMostActiveCanvas();
            if (desired != null)
                parentCanvas = desired; // mettre à jour la référence pour prochaines fois
        }

        if (instance != null)
        {
            // reparenter si nécessaire
            if (desired != null)    
                instance.transform.SetParent(desired.transform, false);
            else
                instance.transform.SetParent(null, false);

            // actualiser rect transform référence
            instanceRect = instance.GetComponent<RectTransform>();

            // ASSURANCE D'ÉCHELLE : forcer scale = 1 après reparent
            if (instanceRect != null)
            {
                instanceRect.localScale = Vector3.one;
                instanceRect.SetAsLastSibling();
            }
            else
            {
                instance.transform.localScale = Vector3.one;
            }

            // Synchroniser le Canvas de l'instance avec le Canvas parent si on force le Canvas
            if (forceCursorCanvas)
            {
                var instCanvas = instance.GetComponent<Canvas>();
                Canvas parentC = desired;
                if (instCanvas == null)
                    instCanvas = instance.AddComponent<Canvas>();

                instCanvas.overrideSorting = true;
                instCanvas.sortingOrder = cursorSortingOrder;

                if (parentC != null)
                {
                    // aligner renderMode / camera / scaleFactor pour éviter différences d'échelle
                    instCanvas.renderMode = parentC.renderMode;
                    if (parentC.renderMode != RenderMode.ScreenSpaceOverlay)
                        instCanvas.worldCamera = parentC.worldCamera;
                    try
                    {
                        instCanvas.scaleFactor = parentC.scaleFactor;
                    }
                    catch
                    {
                        // Certaines versions peuvent bloquer l'écriture : ignore silencieusement
                    }

                    // Si un CanvasScaler existe sur l'instance, forcer ConstantPixelSize pour stabilité
                    var cs = instance.GetComponent<CanvasScaler>();
                    if (cs == null)
                        cs = instance.AddComponent<CanvasScaler>();

                    if (cs != null)
                    {
                        cs.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                        cs.referencePixelsPerUnit = 100f;
                    }
                }
                else
                {
                    // Pas de parentCanvas : set overlay by default
                    instCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
        }

        if (!instance.activeSelf)
            instance.SetActive(true);

        // Remplacer le curseur système par une texture transparente si demandé
        if (replaceSystemCursor && !cursorReplaced)
            ApplyBlankCursor();

        if (hideSystemCursorWhenShown)
            Cursor.visible = false;

        Vector2 mousePos = GetMousePosition();
        UpdateInstancePosition(mousePos);
    }

    // Masque et arrête l'animation
    public void Hide()
    {
        if (instance == null) return;
        StopAnimation();
        if (instance.activeSelf)
            instance.SetActive(false);

        // restaurer le curseur natif
        if (replaceSystemCursor && cursorReplaced)
            RestoreSystemCursor();

        if (hideSystemCursorWhenShown)
            Cursor.visible = true;
    }

    // Joue uniquement l'animation (utile pour hover), sans cacher/afficher le prefab
    public void PlayHoverAnimation()
    {
        EnsureInstance();
        if (!instance.activeSelf)
            instance.SetActive(true);

        PlayAnimation();

        if (replaceSystemCursor && !cursorReplaced)
            ApplyBlankCursor();
    }

    // Arrête uniquement l'animation (utile quand on sort du hover), sans masquer le prefab
    public void StopHoverAnimation()
    {
        if (instance == null) return;
        StopAnimation();
    }

    // Permet à un gestionnaire externe (ex : GameStateManager) de forcer/reporter le Canvas parent
    public void SetParentCanvas(Canvas canvas)
    {
        parentCanvas = canvas;
        if (instance != null && parentCanvas != null)
        {
            instance.transform.SetParent(parentCanvas.transform, false);
            instanceRect = instance.GetComponent<RectTransform>();
            if (instanceRect != null) instanceRect.localScale = Vector3.one;
        }
    }

    // Initialise l'instance si nécessaire (factorise la logique d'instanciation)
    private void EnsureInstance()
    {
        if (cursorPrefab == null) return;
        if (instance != null) return;

        // Instancier d'abord sous le canvas choisi si possible (sinon null parent)
        if (parentCanvas != null && parentCanvas.gameObject.activeInHierarchy)
            instance = Instantiate(cursorPrefab, parentCanvas.transform, false);
        else
            instance = Instantiate(cursorPrefab);

        instanceRect = instance.GetComponent<RectTransform>();
        instanceAnimator = instance.GetComponent<Animator>();

        // sécurité : s'assurer d'une échelle correcte
        if (instanceRect != null)
            instanceRect.localScale = Vector3.one;
        else
            instance.transform.localScale = Vector3.one;

        // s'assurer que l'instance est au-dessus si c'est dans un Canvas
        if (instanceRect != null)
            instanceRect.SetAsLastSibling();

        // Empêcher le prefab d'intercepter les raycasts UI (évite le clignotement)
        CanvasGroup cg = instance.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = instance.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Désactiver les raycastTarget sur tous les Graphic (Images, Text...)
        var graphics = instance.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
            g.raycastTarget = false;

        // Placer sur la layer Ignore Raycast si elle existe (prévention pour SpriteRenderer)
        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreLayer != -1)
            SetLayerRecursively(instance.transform, ignoreLayer);

        // Alerter si le prefab n'a pas d'élément visible
        bool hasVisual = instance.GetComponentInChildren<Renderer>() != null
                         || instance.GetComponentInChildren<CanvasRenderer>() != null
                         || instance.GetComponentInChildren<UnityEngine.UI.Graphic>() != null;
        if (!hasVisual)
        {
            Debug.LogWarning("[CursorPrefabManager] Le prefab du curseur ne contient ni Renderer ni UI Graphic. Il ne sera pas visible.");
        }

        // IMPORTANT : s'assurer que l'Animator est désactivé par défaut (état de base sans animation)
        if (instanceAnimator != null)
        {
            instanceAnimator.enabled = false;
        }

        // --- NOUVEAU : forcer un Canvas sur l'instance du curseur pour garantir qu'il soit au-dessus ---
        if (forceCursorCanvas)
        {
            var c = instance.GetComponent<Canvas>();
            if (c == null) c = instance.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = cursorSortingOrder;

            // ajouter GraphicRaycaster si nécessaire (n'interfère pas si CanvasGroup.blocksRaycasts = false sur children)
            if (instance.GetComponent<GraphicRaycaster>() == null)
                instance.AddComponent<GraphicRaycaster>();
        }

        // s'assurer que l'Animator (si présent) commence désactivé
        if (instanceAnimator != null)
            instanceAnimator.enabled = false;
    }

    private void PlayAnimation()
    {
        if (instanceAnimator != null)
        {
            instanceAnimator.enabled = true;
            instanceAnimator.Play(0, -1, 0f);
        }
    }

    private void StopAnimation()
    {
        if (instanceAnimator != null)
        {
            instanceAnimator.Play(0, -1, 0f);
            instanceAnimator.Update(0f);
            instanceAnimator.enabled = false;
        }
    }

    // centralise la position de l'instance pour éviter les sauts
    private void UpdateInstancePosition(Vector2 mousePos)
    {
        if (instanceRect != null)
        {
            // Si on a un Canvas parent en Screen Space Overlay
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                instanceRect.position = new Vector3(mousePos.x + screenOffset.x, mousePos.y + screenOffset.y, instanceRect.position.z);
                instanceRect.SetAsLastSibling();
                return;
            }

            // Sinon tenter de convertir dans le local du canvas (fiable pour ScreenSpace-Camera ou World)
            Camera cam = (parentCanvas != null && parentCanvas.worldCamera != null) ? parentCanvas.worldCamera : Camera.main;
            RectTransform canvasRect = parentCanvas != null ? parentCanvas.GetComponent<RectTransform>() : null;
            Vector2 localPoint;
            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mousePos, cam, out localPoint))
            {
                instanceRect.anchoredPosition = localPoint + screenOffset;
                instanceRect.SetAsLastSibling();
                return;
            }

            // fallback : position en world via camera
            Camera camToUse = cam ?? Camera.main;
            if (camToUse != null)
            {
                Vector3 worldPos = camToUse.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, camToUse.nearClipPlane + 0.1f));
                instanceRect.position = worldPos + (Vector3)screenOffset;
            }
            else
            {
                instanceRect.position = new Vector3(mousePos.x + screenOffset.x, mousePos.y + screenOffset.y, instanceRect.position.z);
            }
        }
        else if (instance != null)
        {
            // si pas UI, position en world
            Vector3 pos = new Vector3(mousePos.x, mousePos.y, 10f);
            Camera cam = Camera.main;
            if (cam != null)
                instance.transform.position = cam.ScreenToWorldPoint(pos);
            else
                instance.transform.position = pos;
        }
    }

    // Abstraction pour récupérer la position de la souris compatible ancien/nouveau Input System
    private Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayerRecursively(child, layer);
    }

    private void ApplyBlankCursor()
    {
        if (blankCursor == null)
        {
            blankCursor = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            blankCursor.SetPixel(0, 0, new Color(0, 0, 0, 0));
            blankCursor.Apply();
            blankCursor.name = "blank_cursor";
        }

        Cursor.SetCursor(blankCursor, Vector2.zero, CursorMode.Auto);
        Cursor.visible = false;
        cursorReplaced = true;
    }

    private void RestoreSystemCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
        cursorReplaced = false;
    }

    void OnDestroy()
    {
        // restaurer au cas où
        if (cursorReplaced)
            RestoreSystemCursor();
    }

    // Trouve le Canvas actif (activeInHierarchy et enabled) ayant le plus grand sortingOrder.
    private Canvas FindTopMostActiveCanvas()
    {
        var canvases = FindObjectsOfType<Canvas>().Where(c => c.gameObject.activeInHierarchy && c.enabled).ToArray();
        if (canvases == null || canvases.Length == 0) return null;

        // préférer ScreenSpaceOverlay s'il y en a
        var overlay = canvases.Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        if (overlay.Length > 0)
            canvases = overlay;

        Canvas top = canvases.OrderByDescending(c => c.sortingOrder).FirstOrDefault();
        return top;
    }
}