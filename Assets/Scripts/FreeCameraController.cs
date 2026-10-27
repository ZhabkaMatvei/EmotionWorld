
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 15f;
    public float fastMultiplier = 3f;
    public float verticalSpeed = 10f;

    [Header("Mouse")]
    public float mouseSensitivity = 3f;
    public float scrollSensitivity = 3f;

    [Header("Limits")]
    public float minSpeed = 2f;
    public float maxSpeed = 100f;

    private float rotationX;
    private float rotationY;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;

        rotationX = angles.x;
        rotationY = angles.y;
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleScroll();
    }

    private void HandleMouseLook()
    {
        // Осмотр только при зажатой правой кнопке мыши
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X")
                * mouseSensitivity;

            float mouseY = Input.GetAxis("Mouse Y")
                * mouseSensitivity;

            rotationY += mouseX;
            rotationX -= mouseY;

            if (rotationX > 180f)
                rotationX -= 360f;

            rotationX = Mathf.Clamp(
                rotationX, -85f, 85f
            );

            transform.rotation = Quaternion.Euler(
                rotationX,
                rotationY,
                0f
            );
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Перемещение по направлению камеры
        Vector3 direction =
            transform.forward * vertical +
            transform.right * horizontal;

        // Подъём и спуск
        float upDown = 0f;

        if (Input.GetKey(KeyCode.Space))
            upDown += 1f;

        if (Input.GetKey(KeyCode.LeftControl))
            upDown -= 1f;

        direction += Vector3.up *
            upDown * (verticalSpeed / moveSpeed);

        direction = Vector3.ClampMagnitude(
            direction, 1f
        );

        float currentSpeed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= fastMultiplier;
        }

        transform.position +=
            direction * currentSpeed * Time.deltaTime;
    }

    private void HandleScroll()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.001f)
        {
            moveSpeed += scroll * scrollSensitivity * 10f;

            moveSpeed = Mathf.Clamp(
                moveSpeed,
                minSpeed,
                maxSpeed
            );
        }
    }
}
