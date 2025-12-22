using UnityEngine;

public class SpawnedRock : MonoBehaviour
{
    public enum Category
    {
        Normal,
        Halloween,
        Christmas
    }

    public Category category = Category.Normal;
}