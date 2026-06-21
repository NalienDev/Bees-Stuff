using UnityEngine;

public class HotbarSlot : MonoBehaviour
{
    public bool isActive;
    [SerializeField] private GameObject _highlightImage;

    void Update()
    {
        if (isActive)
        {
            _highlightImage.SetActive(true);
        }
        else
        {
            _highlightImage.SetActive(false);
        }
    }
}
