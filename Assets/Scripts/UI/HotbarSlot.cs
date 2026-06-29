using UnityEngine;

public class HotbarSlot : MonoBehaviour
{
    public bool isActive;
    [SerializeField] private GameObject highlightImage;

    void Update()
    {
        if (isActive)
        {
            highlightImage.SetActive(true);
        }
        else
        {
            highlightImage.SetActive(false);
        }
    }
}
