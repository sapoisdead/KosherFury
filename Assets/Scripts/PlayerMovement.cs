using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private Vector2 moveInput;
    private bool isSprinting;
    private bool jumpPressed;
    private bool elevatePlayer;
    private Vector3 velocity;
    private Transform cameraTransform;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Chiamati da PlayerInput (Send Messages)
    private void OnRun(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        if (moveInput.sqrMagnitude > 0.01f)
            GetComponent<PlayerAnimator>()?.ResetPunch();
    }

    private void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    private void OnJump(InputValue value)
    {
        if (value.isPressed && controller.isGrounded)
        {
            jumpPressed = true;
            GetComponent<PlayerAnimator>()?.TriggerJump();
        }
    }

    private void OnPunch(InputValue value)
    {
        if (!value.isPressed) return;
        GetComponent<PlayerAnimator>()?.TriggerPunch();
    }

    private void OnElevating()
    {
        elevatePlayer = true;
    }

    private void Update()
    {
        HandleGravityAndJump();
        HandleMovement();
    }

    private void HandleGravityAndJump()
    {
        if (controller.isGrounded)
        {
            if (velocity.y < 0f)
                velocity.y = -2f;

            if (elevatePlayer)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        jumpPressed = false;
        elevatePlayer = false;
        velocity.y += gravity * Time.deltaTime;
    }

    private void HandleMovement()
    {
        if (moveInput.sqrMagnitude > 0.01f)
        {
            float speed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

            if (moveDir != Vector3.zero)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(moveDir),
                    360f * Time.deltaTime
                );
            }

            controller.Move((moveDir * speed + velocity) * Time.deltaTime);
        }
        else
        {
            controller.Move(velocity * Time.deltaTime);
        }
    }

    public float GetSpeed()
    {
        Vector3 horizontal = controller.velocity;
        horizontal.y = 0f;
        return horizontal.magnitude;
    }
}
