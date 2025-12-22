using UnityEngine;

public class SpawnedTree : MonoBehaviour
{
    public enum Category
    {
        Normal,
        Halloween,
        Christmas
    }

    public Category category = Category.Normal;
}