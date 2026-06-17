using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using NUnit.Framework;
[RequireComponent(typeof(CharacterController))]
public class PreyAgent : Agent
{
    [Header("Coordenação")]
    public PreyPredatorArena arena;
    [Header("Movimento")]
    public float moveSpeed = 4f;
    public float gravity = 20f;
    [SerializeField] private CharacterController controller;
    private Vector3 velocity;
    public override void Initialize()
    {
        controller = GetComponent<CharacterController>();
        controller.stepOffset = 1.0f; // consistente com mundo voxel (aula 08)
    }
    public override void OnEpisodeBegin()
    {
        velocity = Vector3.zero;
        // A presa é o "episode owner" — pede à arena para respawnar ambos
        arena.StartEpisode();
    }
    public void Place(Vector3 localPosition)
    {
        controller.enabled = false;
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        controller.enabled = true;
        velocity = Vector3.zero;
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        // Velocidade própria normalizada (XZ)
        sensor.AddObservation(velocity.x / moveSpeed); // 1
        sensor.AddObservation(velocity.z / moveSpeed); // 1
                                                       // Total: 2 floats. Ray Perception Sensors entregam o resto.
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Sem reward shaping — recompensa final pela arena
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        Vector3 horizontal = new Vector3(moveX, 0f, moveZ) * moveSpeed;
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y -= gravity * Time.deltaTime;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;
        controller.Move(velocity * Time.deltaTime);
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
    }
}