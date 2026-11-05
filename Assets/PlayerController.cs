using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;

    private InputAction moveAction;
    private Vector2 moveInput;

    void Start()
    {
        // Yeni Input Sistemi ile kod üzerinden WASD ataması oluşturuyoruz
        moveAction = new InputAction("Move");
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        moveAction.Enable(); // Aksiyonu aktifleştir
    }

    void Update()
    {
        // WASD tuşlarından gelen değeri (X ve Y) oku
        moveInput = moveAction.ReadValue<Vector2>();

        // 3D dünyada X ve Z ekseninde hareket et
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        transform.Translate(move * speed * Time.deltaTime, Space.World);
    }

    void OnDestroy()
    {
        // Obje yok olduğunda memory leak olmaması için aksiyonu kapat
        moveAction.Disable();
    }
}