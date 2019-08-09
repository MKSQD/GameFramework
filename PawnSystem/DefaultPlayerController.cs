using Cube.Transport;

namespace GameFramework {
    public class DefaultPlayerController : PlayerController {
        public DefaultPlayerController(Connection connection) : base(connection) {
        }

        public override void SetupInputComponent() {
            input.BindAxis("Mouse X", OnMouseXInput);
            input.BindAxis("Mouse Y", OnMouseYInput);
            input.BindAxis("Horizontal", OnHorizontalInput);
            input.BindAxis("Vertical", OnVerticalInput);
            input.BindAction("Jump", OnJumpInput);
        }

        void OnHorizontalInput(float value) {
            character.movement.AddMoveInput(character.transform.right * value);
        }

        void OnVerticalInput(float value) {
            character.movement.AddMoveInput(character.transform.forward * value);
        }

        void OnMouseXInput(float value) {
            character.movement.AddYawInput(value);
        }

        void OnMouseYInput(float value) {
            character.movement.AddPitchInput(value);
        }

        void OnJumpInput() {
            character.movement.Jump();
        }
    }
}