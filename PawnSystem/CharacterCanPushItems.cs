using UnityEngine;

[RequireComponent(typeof(Character))]
public class CharacterCanPushItems : MonoBehaviour {
    float pushPower = 2f;

    void OnControllerColliderHit(ControllerColliderHit hit) {
        var body = hit.collider.attachedRigidbody;


        // no rigidbody
        if (body == null || body.isKinematic)
            return;

        // We don't want to push objects below us
        if (hit.moveDirection.y < -0.3f)
            return;

        // Calculate push direction from move direction,
        // we only push objects to the sides never up and down
        var pushDir = hit.moveDirection;
        pushDir.y = 0;

        // If you know how fast your character is trying to move,
        // then you can also multiply the push velocity by that.
        // Apply the push
        body.velocity = pushDir * pushPower;
    }
}
