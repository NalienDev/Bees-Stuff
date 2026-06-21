using UnityEngine;


public class FlowerController : MonoBehaviour
{
    [Header("Settings de Cooldown")]

    public float rechargeDuration = 50f;

    [Header("Feedback Visual")]

    public MeshRenderer flowerRenderer;

    public Material chargedMaterial;

    public Material depletedMaterial;

    private bool isCharged = true;
    private float rechargeTimer = 0f;


    public bool IsCharged => isCharged;

    public MonoBehaviour ReservedBy { get; set; }

    private void Awake()
    {


        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
            {
                Destroy(col);
            }
        }
    }


    private void Update()
    {
        if (!isCharged)
        {
            rechargeTimer -= Time.deltaTime;
            if (rechargeTimer <= 0f)
            {
                isCharged = true;
                UpdateVisual();
            }
        }
    }


    public bool TryCollectPollen()
    {
        if (!isCharged) return false;

        isCharged = false;
        ReservedBy = null;
        rechargeTimer = rechargeDuration;
        UpdateVisual();
        return true;
    }


    public void ResetFlower()
    {
        isCharged = true;
        rechargeTimer = 0f;
        ReservedBy = null;
        UpdateVisual();
    }


    private void UpdateVisual()
    {
        if (flowerRenderer == null) return;
        flowerRenderer.material = isCharged ? chargedMaterial : depletedMaterial;
    }
}
