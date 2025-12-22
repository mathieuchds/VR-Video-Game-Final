using UnityEngine;

public class SpawnedSeasonal : MonoBehaviour
{
    public enum Category
    {
        Halloween,
        Winter
    }

    public Category category = Category.Halloween;
    public string prefabName;
    public bool activated = false;
}