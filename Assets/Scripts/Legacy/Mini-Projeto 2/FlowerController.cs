using UnityEngine;

/**
 * Controla o estado individual de uma flor.
 * Gere a disponibilidade de pólen, o tempo de recarga após a extração 
 * e a respetiva atualização visual do modelo.
 */
public class FlowerController : MonoBehaviour
{
    [Header("Settings de Cooldown")]
    /** Tempo necessário (em segundos) para a flor voltar a ter pólen após ser colhida. */
    public float rechargeDuration = 50f;

    [Header("Feedback Visual")]
    /** Referência para o renderizador do modelo da flor. */
    public MeshRenderer flowerRenderer;
    /** Material aplicado quando a flor possui pólen. */
    public Material chargedMaterial;
    /** Material aplicado quando a flor está vazia (em recarga). */
    public Material depletedMaterial;

    private bool isCharged = true;
    private float rechargeTimer = 0f;

    /** Indica se a flor possui pólen disponível para colheita. */
    public bool IsCharged => isCharged;
    /** Regista a abelha que tenciona colher esta flor, prevenindo a concorrência. */
    public MonoBehaviour ReservedBy { get; set; }

    private void Awake()
    {
        // Garante que o prefab da flor (usado puramente para lógica no mundo Voxel)
        // não tem colisores sólidos que bloqueiem o jogador (blocos invisíveis).
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
            {
                Destroy(col);
            }
        }
    }

    /**
     * Atualiza o temporizador de recarga a cada frame.
     * Restaura a carga da flor e atualiza o seu material quando o tempo expira.
     */
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

    /**
     * Tenta extrair pólen desta flor.
     * Se bem-sucedido, descarrega a flor, limpa as reservas e inicia o período de recarga.
     * @return Verdadeiro se a colheita foi bem-sucedida, falso caso a flor já esteja vazia.
     */
    public bool TryCollectPollen()
    {
        if (!isCharged) return false;

        isCharged = false;
        ReservedBy = null; // Clear reservation after collection
        rechargeTimer = rechargeDuration;
        UpdateVisual();
        return true;
    }

    /**
     * Repõe imediatamente a flor para o seu estado inicial (carregada).
     * Utilizado aquando da reinicialização da arena de treino.
     */
    public void ResetFlower()
    {
        isCharged = true;
        rechargeTimer = 0f;
        ReservedBy = null; 
        UpdateVisual();
    }

    /**
     * Atualiza o material da flor para refletir o seu estado atual de carga.
     */
    private void UpdateVisual()
    {
        if (flowerRenderer == null) return;
        flowerRenderer.material = isCharged ? chargedMaterial : depletedMaterial;
    }
}
