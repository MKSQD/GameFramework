public class DefaultPlayerController : PlayerController {
    public override void SetupInputComponent() {
        input.BindAxis("Horizontal", OnHorizontalInput);
        input.BindAxis("Vertical", OnVerticalInput);
        input.BindAxis("Mouse X", OnMouseXInput);
        input.BindAxis("Mouse Y", OnMouseYInput);
        input.BindAction("Jump", OnJumpInput);
    }

    void OnHorizontalInput(float value) {
        character.AddMovementInput(character.transform.right * value);
    }

    void OnVerticalInput(float value) {
        character.AddMovementInput(character.transform.forward * value);
    }

    void OnMouseXInput(float value) {
        character.AddYawInput(value);
    }

    void OnMouseYInput(float value) {
        character.AddPitchInput(value);
    }

    void OnJumpInput() {
        character.Jump();
    }
}
