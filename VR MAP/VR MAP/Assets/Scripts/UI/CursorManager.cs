using System.Collections.Generic;
using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Curseurs")]
    [Tooltip("Drag ton asset .asset, Sprite ou Texture2D ici")]
    public Object defaultCursorAsset;
    [Tooltip("Drag ton asset .asset, Sprite ou Texture2D pour le hover")]
    public Object defaultHoverCursorAsset;

    [Header("Hotspot")]
    [Tooltip("Point de clic (en pixels) à partir du coin supérieur gauche de la texture")]
    public Vector2 defaultHotspot = Vector2.zero;

    private Texture2D defaultCursor;
    private Texture2D defaultHoverCursor;

    private readonly Dictionary<Object, Texture2D> assetToTextureCache = new Dictionary<Object, Texture2D>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        defaultCursor = ResolveToTexture(defaultCursorAsset);
        defaultHoverCursor = ResolveToTexture(defaultHoverCursorAsset);

        if (defaultCursor == null)
            Debug.LogWarning("[CursorManager] defaultCursorAsset n'a pas pu être converti en Texture2D. Assure-toi que c'est une Texture2D ou un Sprite importé et que la texture source est Read/Write Enabled.");
        if (defaultHoverCursor == null)
            Debug.LogWarning("[CursorManager] defaultHoverCursorAsset n'a pas pu être converti en Texture2D. Assure-toi que c'est une Texture2D ou un Sprite importé et que la texture source est Read/Write Enabled.");

        ApplyDefault();
    }

    public void ApplyDefault()
    {
        SetCursor(defaultCursor, defaultHotspot);
    }


    public void SetCursor(Texture2D tex, Vector2 hotspot)
    {
        if (tex == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }
        Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
    }


    public void SetCursorFromAsset(Object asset, Vector2 hotspot)
    {
        var tex = ResolveToTexture(asset);
        SetCursor(tex, hotspot);
    }


    public void SetHoverCursor(Object hoverAsset, Vector2 hotspot)
    {
        var tex = ResolveToTexture(hoverAsset) ?? defaultHoverCursor;
        SetCursor(tex, hotspot);
    }

    public void ResetToDefault()
    {
        ApplyDefault();
    }


    private Texture2D ResolveToTexture(Object asset)
    {
        if (asset == null) return null;

        if (assetToTextureCache.TryGetValue(asset, out var cached))
            return cached;

        Texture2D result = null;

        if (asset is Texture2D t2)
        {
            result = t2;
        }
        else if (asset is Sprite sp)
        {
            Texture2D src = sp.texture;
            if (src == null)
            {
                Debug.LogWarning($"[CursorManager] Sprite '{sp.name}' n'a pas de texture source.");
                assetToTextureCache[asset] = null;
                return null;
            }

            try
            {
                // Texture2D.GetPixel ne lancera pas d'exception ici mais GetPixels peut
                Rect texRect = sp.textureRect;
                int w = Mathf.RoundToInt(texRect.width);
                int h = Mathf.RoundToInt(texRect.height);
                int x = Mathf.RoundToInt(texRect.x);
                int y = Mathf.RoundToInt(texRect.y);

                // Extraire les pixels du sprite (nécessite Read/Write Enabled dans l'import)
                Color[] pixels = src.GetPixels(x, y, w, h);
                result = new Texture2D(w, h, TextureFormat.RGBA32, false);
                result.SetPixels(pixels);
                result.Apply();
                result.name = sp.name + "_cursor_cropped";

                // Calquer les propriétés utiles
                result.filterMode = src.filterMode;
                result.wrapMode = TextureWrapMode.Clamp;
            }
            catch (UnityException e)
            {
                Debug.LogWarning($"[CursorManager] Impossible d'extraire Sprite '{sp.name}' depuis son atlas : la texture source doit être Read/Write Enabled. Exception: {e.Message}");
                result = null;
            }
        }

        else if (asset is GameObject go)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                result = ResolveToTexture(sr.sprite);
            else
            {
                var img = go.GetComponent<UnityEngine.UI.Image>();
                if (img != null && img.sprite != null)
                    result = ResolveToTexture(img.sprite);
            }
        }
        else
        {
            result = null;
        }

        assetToTextureCache[asset] = result;
        return result;
    }
}
