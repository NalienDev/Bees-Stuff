using NUnit.Framework;
using System;
using UnityEngine;
public class PreyPredatorArena : MonoBehaviour
{
    [Header("Agentes")]
    public PreyAgent prey;
    public PredatorAgent predator;
    [Header("Limites")]
    public int maxEpisodeSteps = 2000;
    public float arenaHalfSize = 4.5f;
    [Header("Feedback visual (opcional)")]
    public MeshRenderer floorRenderer;
    public Material defaultMaterial;
    public Material predatorWinMaterial;
    public Material preyWinMaterial;
    private int stepCount;
    public void StartEpisode()
    {
        // Respawnar ambos os agentes em lados opostos da arena
        
        stepCount = 0;

        float x = arenaHalfSize;
        float z = arenaHalfSize;

        // Presa: Z positivo; Predador: Z negativo
        prey.Place(new Vector3(
             UnityEngine.Random.Range(-x, x),
             1.12f,
             UnityEngine.Random.Range(0f, z)
        ));

        predator.Place(new Vector3(
             UnityEngine.Random.Range(-x, x),
             1.12f,
             UnityEngine.Random.Range(-z, 0f)
        ));

        SetFloor(defaultMaterial);
    }
    private void FixedUpdate()
    {
        stepCount++;
        // Se stepCount >= maxEpisodeSteps → timeout (presa ganha)
        if (stepCount >= maxEpisodeSteps)
        {
            prey.AddReward(+1f);
            SetFloor(preyWinMaterial);
            EndAndReset();
        }
    }
    public void OnPreyCaptured()
    {
        // Predador tocou na presa: +1 predador, ‐1 presa
        predator.AddReward(+1f);
        prey.AddReward(-1f);
        SetFloor(predatorWinMaterial);
        EndAndReset();
    }
    private void EndAndReset()
    {
        // Chamar EndEpisode em ambos os agentes
        prey.EndEpisode();
        predator.EndEpisode();
    }

    private void SetFloor(Material mat)
    {
        if (floorRenderer != null && mat != null)
            floorRenderer.material = mat;
    }
}