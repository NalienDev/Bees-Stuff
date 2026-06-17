using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using NUnit.Framework;
[RequireComponent(typeof(CharacterController))]
public class PredatorAgent : Agent
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
        controller.stepOffset = 1.0f;
    }
    public override void OnEpisodeBegin()
    {
        // NÃO chama arena.StartEpisode — a presa trata disso
        velocity = Vector3.zero;
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
        // Mesmas observações que a presa — jogo simétrico
        sensor.AddObservation(velocity.x / moveSpeed);
        sensor.AddObservation(velocity.z / moveSpeed);
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
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
private void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Prey"))
    {
        arena.OnPreyCaptured();
    }
}
}