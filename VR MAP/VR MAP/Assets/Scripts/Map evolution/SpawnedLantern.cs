using UnityEngine;

public class SpawnedLantern : MonoBehaviour
{
    public enum Category
    {
        Normal,
        Halloween,
        Christmas
    }

    public Category category = Category.Normal;
}