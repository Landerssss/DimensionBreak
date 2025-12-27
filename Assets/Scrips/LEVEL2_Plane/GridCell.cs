using UnityEngine;

public class GridCell : MonoBehaviour
{
    public GameObject currentPlant;
    public string currentPlantTag;

    public void SetPlant(GameObject plant, string tag)
    {
        currentPlant = plant;
        currentPlantTag = tag;
    }
}
