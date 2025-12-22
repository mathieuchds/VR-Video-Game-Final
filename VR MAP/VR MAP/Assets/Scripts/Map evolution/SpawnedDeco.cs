using UnityEngine;

public class SpawnedDeco : MonoBehaviour
{
    public enum Category
    {
        Normal,
        Halloween,
        Christmas
    }

    public Category category = Category.Normal;
}