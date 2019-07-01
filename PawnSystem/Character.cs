using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Character : Pawn {
    public Transform view;
    public float moveSpeed = 1;
    public float jumpForce = 12;
    public float airControl = 0.1f;

    public Vector3 velocity {
        get { return _cc.velocity; }
    }

    CharacterController _cc;
    Vector3 _movement;
    Vector3 _lastMovement;
    float _yaw;
    float _pitch;
    float _jumpForce;

    protected virtual void Start() {
        _cc = GetComponent<CharacterController>();
    }

    public override void AddMovementInput(Vector3 worldDirection) {
        _movement += worldDirection.normalized;
    }

    public override void AddYawInput(float value) {
        _yaw += value;
        _yaw = Mathf.Repeat(_yaw, 360);
    }

    public override void AddPitchInput(float value) {
        _pitch += value;
        _pitch = Mathf.Clamp(_pitch, -60, 50);
    }

    public void Jump() {
        if (!_cc.isGrounded)
            return;

        _jumpForce = 1;
    }

    public override void Tick() {
        base.Tick();

        var actualMovement = _movement * moveSpeed;

        // Air Control
        if (!_cc.isGrounded) {
            actualMovement = Vector3.Lerp(_lastMovement, actualMovement, airControl);
        }

        _lastMovement = actualMovement;

        // Jump
        if (_jumpForce > 0.4f) {
            _jumpForce *= 0.98f;

            actualMovement += _jumpForce * Vector3.up * jumpForce;
        }

        // Gravity
        if (!_cc.isGrounded) {
            actualMovement += Physics.gravity;
        }

        _cc.Move(actualMovement * Time.deltaTime);

        // Rotation
        transform.localRotation = Quaternion.AngleAxis(_yaw, Vector3.up);
        view.localRotation = Quaternion.AngleAxis(_pitch, Vector3.left);

        _movement = Vector3.zero;
    }
}
