using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Basit oyuncu hareketi (WASD / ok tuşları).
/// CharacterController kullanır: duvarların içinden geçmez, yer çekimi ile zemine basar.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    public float speed = 8f;
    [Tooltip("Hareket yönüne dönme hızı (derece/sn).")]
    public float turnSpeed = 720f;
    public float gravity = -20f;

    private CharacterController controller;
    private InputAction moveAction;
    private float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Yeni Input Sistemi ile kod üzerinden WASD + ok tuşları ataması
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    void OnEnable() => moveAction.Enable();
    void OnDisable() => moveAction.Disable();
    void OnDestroy() => moveAction.Dispose();

    void Update()
    {
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f); // çaprazda hızlanmasın
        Vector3 move = new Vector3(input.x, 0f, input.y);

        // Hareket yönüne dön (düşmanın "görüş" mantığıyla tutarlı görünsün)
        if (move.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        // Yer çekimi
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}
