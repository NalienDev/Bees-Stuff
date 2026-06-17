using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
[RequireComponent(typeof(CharacterController))]
public class StairsAgent : Agent
{
    public Transform targetTransform;
    public Transform platformTransform;
    public MeshRenderer floorRenderer;
    public Material defaultMaterial, winMaterial, loseMaterial;
    public float moveSpeed = 4f;
    public float gravity = 20f;
    private CharacterController controller;
    private Vector3 velocity;
    public override void Initialize()
    {
        controller = GetComponent<CharacterController>();
        controller.stepOffset = 1.0f; // sobe degraus até 1m sem saltar
    }
    public override void OnEpisodeBegin()
    {
        // CharacterController não aceita atribuições directas de posição
        // durante o update — desactivar → mover → reactivar.
        controller.enabled = false;
        transform.localPosition = new Vector3(
        Random.Range(-3.5f, 3.5f), 1f, Random.Range(-3.5f, -1.5f));
        controller.enabled = true;
        velocity = Vector3.zero;
        if (floorRenderer != null) floorRenderer.material = defaultMaterial;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = targetTransform.localPosition - transform.localPosition;
        sensor.AddObservation(toTarget.normalized); // 3
        sensor.AddObservation(toTarget.magnitude / 10f); // 1
        sensor.AddObservation(controller.isGrounded ? 1f : 0f); // 1
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        AddReward(-1f / MaxStep); // time penalty total ≈ ‐1 se nada acontecer
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        Vector3 horizontal = new Vector3(moveX, 0f, moveZ) * moveSpeed;
        // Gravidade manual (CharacterController não a aplica sozinho).
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y -= gravity * Time.deltaTime;
        Vector3 total = horizontal + Vector3.up * velocity.y;
        controller.Move(total * Time.deltaTime);
        // Caiu para fora da arena?
        if (transform.localPosition.y < -2f)
{
            AddReward(-1f);
            if (floorRenderer != null) floorRenderer.material = loseMaterial;
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            AddReward(+1f);
            if (floorRenderer != null) floorRenderer.material = winMaterial;
            EndEpisode();
        }
        else if (other.CompareTag("Wall"))
        {
            AddReward(-1f);
            if (floorRenderer != null) floorRenderer.material = loseMaterial;
            EndEpisode();
        }
    }
}
