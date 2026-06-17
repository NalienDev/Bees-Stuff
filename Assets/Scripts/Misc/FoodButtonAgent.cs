using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
public class FoodButtonAgent : Agent
{
    [Header("Referências da cena")]
    public Transform buttonTransform;
    public Transform foodTransform;
    public MeshRenderer floorRenderer;

    [Header("Feedback visual")]
    public Material winMaterial;
    public Material loseMaterial;

    [Header("Parâmetros")]
    public float moveSpeed = 5f;

    private bool buttonPressed = false;

    // 1. OnEpisodeBegin: Reinicia o estado no início de cada episódio
    public override void OnEpisodeBegin()
    {
        // Agente: posição aleatória
        transform.localPosition = new Vector3(
            Random.Range(-3.5f, 3.5f), 0.5f, Random.Range(-3.5f, 3.5f));

        // Botão: posição fixa
        buttonTransform.localPosition = new Vector3(
            Random.Range(-3.5f, 3.5f), 0.5f, Random.Range(-3.5f, 3.5f));

        // Comida: desactivada, posição aleatória (para quando for activada) 
        foodTransform.gameObject.SetActive(false);
        foodTransform.localPosition = RandomFoodPosition();

        buttonPressed = false;
    }

    private Vector3 RandomFoodPosition()
    {
        return new Vector3(
            Random.Range(-3.5f, 3.5f), 0.5f, Random.Range(-3.5f, 3.5f)); 
    }

    // 2. CollectObservations: O que o agente precisa de saber para decidir
    public override void CollectObservations(VectorSensor sensor)
    {
        // Sempre: direcção e distância ao botão
        Vector3 dirBtn = (buttonTransform.localPosition - transform.localPosition).normalized;
        float distBtn = Vector3.Distance(buttonTransform.localPosition, transform.localPosition) / 10f;
        sensor.AddObservation(dirBtn);  // 3 floats 
        sensor.AddObservation(distBtn); // 1 float

        // Condicional: direcção e distância à comida
        if (foodTransform.gameObject.activeSelf)
        {
            Vector3 dirFood = (foodTransform.localPosition - transform.localPosition).normalized;
            float distFood = Vector3.Distance(foodTransform.localPosition, transform.localPosition) / 10f;
            sensor.AddObservation(dirFood);  // 3 floats
            sensor.AddObservation(distFood); // 1 float
        }
        else
        {
            sensor.AddObservation(Vector3.zero); // 3 floats (placeholder)
            sensor.AddObservation(0f);           // 1 float (placeholder)
        }
    }

    // 3. OnActionReceived: Processa as decisões (movimento e interação)
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Time penalty: pequena penalidade por step
        AddReward(-1f / MaxStep); // total: ‐1 se não fizer nada útil
                                  // Contínuas: mover
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        transform.localPosition +=
        new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;
        // Discreta: carregar no botão
        int press = actions.DiscreteActions[0];
        if (press == 1 && !buttonPressed)
        {
            float distToButton = Vector3.Distance(transform.localPosition,
            buttonTransform.localPosition);
            if (distToButton < 1f)
            {
                buttonPressed = true;
                foodTransform.gameObject.SetActive(true);
                AddReward(+0.5f); // recompensa por carregar no botão
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Food"))
        {
            AddReward(+1f); // soma ao que já acumulou
            floorRenderer.material = winMaterial;
            EndEpisode();
        }
        if (other.CompareTag("Wall"))
        {
            AddReward(-1f);
            floorRenderer.material = loseMaterial;
            EndEpisode();
        }
    }

    // 4. Heuristic: Controlo manual para testes
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal"); // A-D / setas
        ca[1] = Input.GetAxis("Vertical");   // W-S / setas

        var da = actionsOut.DiscreteActions;
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0; // espaço = carregar
    }
}