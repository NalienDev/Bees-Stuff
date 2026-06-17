using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class MoveToTarget : Agent
{
    public Transform targetTransform;
    public MeshRenderer floorRenderer;
    public Material winMaterial;
    public Material loseMaterial;
    public float moveSpeed = 5f;

    // No início de cada episódio, colocar o agente e o alvo numa posição aleatória em cada metade da arena.
    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(Random.Range(-4f, 4f), 0.5f, Random.Range(-3.5f, -0.5f));
        targetTransform.localPosition = new Vector3(Random.Range(-4f, 4f), 0.5f, Random.Range(0.5f, 3.5f));
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = (targetTransform.localPosition - transform.localPosition);

        // Adicionar direção para o alvo (normalizada) - 3 floats
        sensor.AddObservation(toTarget.normalized);

        // Adicionar distância ao alvo (normalizada) - 1 float
        // A arena tem cerca de 10x10, a distância máxima é ~14.14, por isso dividimos por 15f
        sensor.AddObservation(toTarget.magnitude / 15f);
    }

    // TODO: Rever
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Ler as duas ações contínuas
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        AddReward(-0.001f); // pequena penalidade por step

        // Mover o agente
        transform.localPosition += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            SetReward(+1f);
            floorRenderer.material = winMaterial;
        }
        if (other.CompareTag("Wall"))
        {
            SetReward(-1f);
            floorRenderer.material = loseMaterial;
        }
        EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
    }
}