using UnityEngine;
using System.Collections.Generic;

/**
 * Gere a instanciação e distribuição espacial das flores na arena.
 * Coordena-se com o HiveManager para garantir que as flores surgem em locais válidos.
 */
public class FlowerManager : MonoBehaviour
{
    /** Prefab utilizado para instanciar novas flores. */
    public GameObject flowerPrefab;
    /** Número de flores a instanciar em cada quadrante válido. */
    public int flowersPerQuarter = 3;

    /** Referência ao gestor global para registar as flores geradas. */
    public HiveManager hiveManager;
    private List<GameObject> spawnedFlowers = new List<GameObject>();

    /**
     * Destrói as flores existentes e instancia um novo conjunto baseado numa lista de áreas.
     * Regista posteriormente o novo conjunto no HiveManager.
     * * @param quarters: Lista de áreas retangulares (quadrantes) onde as flores podem ser instanciadas.
     */
    public void SpawnFlowers(List<Rect> quarters)
    {
        // Limpa flores antigas
        foreach (var flower in spawnedFlowers)
        {
            if (flower != null) Destroy(flower);
        }
        spawnedFlowers.Clear();

        // Instancia novas flores em cada quadrante
        foreach (var rect in quarters)
        {
            for (int i = 0; i < flowersPerQuarter; i++)
            {
                Vector3 spawnPos = new Vector3(
                    Random.Range(rect.xMin, rect.xMax),
                    0.25f,
                    Random.Range(rect.yMin, rect.yMax)
                );

                GameObject flower = Instantiate(flowerPrefab, transform);
                flower.transform.localPosition = spawnPos;
                spawnedFlowers.Add(flower);
            }
        }

        // Ordena ao HiveManager para utilizar estas flores
        if (hiveManager != null)
        {
            List<FlowerController> flowerControllers = new List<FlowerController>();
            foreach (var f in spawnedFlowers)
            {
                flowerControllers.Add(f.GetComponent<FlowerController>());
            }
            hiveManager.SetFlowers(flowerControllers);
        }
    }
}
