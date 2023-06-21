using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestPlayerMovement : MonoBehaviour
{
    public float speed;
    public float lookSpeed;
    private DebugInputs input;

    private float forward, side, up;
    private Vector2 mouseDelta;

    private float xRot, yRot;

    private void Awake()
    {
        input = new DebugInputs();
    }

    private void OnEnable()
    {
        input.Enable();
        input.Player.Movement.performed += OnMovePerformed;
        input.Player.Movement.canceled += OnMoveCancelled;

        input.Player.Look.performed += OnLookPerformed;
        input.Player.Movement.canceled += OnLookCancelled;
    }

    private void OnDisable()
    {
        input.Disable();
        input.Player.Movement.performed -= OnMovePerformed;
        input.Player.Movement.canceled -= OnMoveCancelled;

        input.Player.Look.performed -= OnLookPerformed;
        input.Player.Movement.canceled -= OnLookCancelled;
    }

    private void Update()
    {
        xRot -= mouseDelta.y * Time.deltaTime;
        xRot = Mathf.Clamp(xRot, -70, 70);
        yRot += mouseDelta.x * Time.deltaTime;
        transform.localEulerAngles = new Vector3(xRot, yRot, 0);
        transform.position += (transform.forward * forward + transform.right * side + transform.up * up) * Time.deltaTime;
    }

    private void OnMovePerformed(InputAction.CallbackContext value)
    {
        Vector3 moveDir = value.ReadValue<Vector3>() * speed;
        forward = moveDir.z;
        side = moveDir.x;
        up = moveDir.y;
    }

    private void OnMoveCancelled(InputAction.CallbackContext value)
    {
        forward = side = up = 0;
    }

    private void OnLookPerformed(InputAction.CallbackContext value)
    {
        mouseDelta = value.ReadValue<Vector2>() * lookSpeed;
    }

    private void OnLookCancelled(InputAction.CallbackContext value)
    {
        mouseDelta = Vector2.zero;
    }
}
