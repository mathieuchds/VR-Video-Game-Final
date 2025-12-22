using UnityEngine;

public class SpawnedGrass : MonoBehaviour
{
    public enum Category
    {
        Flower,
        Halloween,
        Winter
    }

    public Category category = Category.Flower;
}