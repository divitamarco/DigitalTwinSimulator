using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class OVRSimpleMovement : MonoBehaviour
{
    public float speed = 2.0f;
    public float gravity = -9.81f;
    private CharacterController controller;
    private Transform cameraTransform;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main != null ? Camera.main.transform : transform;
    }

    void Update()
    {
        if (controller == null || !controller.enabled)
            return; // ❗ Evita Move() su controller inattivo

        if (cameraTransform == null)
        {
            if (Camera.main != null)
                cameraTransform = Camera.main.transform;
            else
                return;
        }

        // Input dallo stick sinistro (Oculus)
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // Direzione di movimento in base alla testa
        Vector3 move = cameraTransform.forward * input.y + cameraTransform.right * input.x;
        move.y = 0f;

        // ❗ Esegui Move solo se l'oggetto è attivo
        if (gameObject.activeInHierarchy)
            controller.Move(move * speed * Time.deltaTime);

        // Gravità
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        if (gameObject.activeInHierarchy)
            controller.Move(velocity * Time.deltaTime);
    }
}
