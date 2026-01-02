using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ForceRayVisible : MonoBehaviour
{
    private LineRenderer line;
    private Transform hand;

    [SerializeField] private float rayLength = 3.0f;
    [SerializeField] private Color rayColor = Color.cyan;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.003f;
        line.endWidth = 0.001f;

        // Crea un materiale base se non ne esiste già uno
        if (line.material == null)
        {
            line.material = new Material(Shader.Find("Unlit/Color"));
        }

        line.material.color = rayColor;
        hand = transform;
    }

    void Update()
    {
        // Punto iniziale: la posizione dell'anchor della mano
        Vector3 start = hand.position;
        // Direzione: in avanti rispetto al controller
        Vector3 end = hand.position + hand.forward * rayLength;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }
}
