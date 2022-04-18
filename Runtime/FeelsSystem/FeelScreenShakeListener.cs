using Cinemachine;
using UnityEngine;

namespace GameFramework.FeelsSystem {
    /// <summary>
    /// An add-on module for Cinemachine to shake the camera.
    /// </summary>
    [ExecuteInEditMode]
    [SaveDuringPlay]
    [AddComponentMenu("")] // Hide in menu
    public class FeelScreenShakeListener : CinemachineExtension {
        [Tooltip("Amplitude of the shake")]
        public float m_Range = 0.5f;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime) {
            if (stage == CinemachineCore.Stage.Body) {
                Vector3 shakeAmount = GetOffset();
                state.PositionCorrection += vcam.transform.rotation * shakeAmount;
            }
        }

        Vector3 GetOffset() {
            var offset = FeelsManager.Main.ScreenShakeOffset;
            return new Vector3(offset.x, offset.y, 0);
        }
    }
}