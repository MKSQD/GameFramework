using Cinemachine;
using UnityEngine;

namespace GameFramework.FeelsSystem {
    /// <summary>
    /// An add-on module for Cinemachine.
    /// </summary>
    [ExecuteInEditMode]
    [SaveDuringPlay]
    [AddComponentMenu("")] // Hide in menu
    public class CinemachineFeelsListener : CinemachineExtension {
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime) {
            if (stage == CinemachineCore.Stage.Body) {
                Vector3 shakeAmount = GetOffset(vcam);
                state.PositionCorrection += shakeAmount;
                state.OrientationCorrection *= ScreenShake.CalculateCurrentRotation() * CameraOffset.CurrentRotation;
            }
        }

        Vector3 GetOffset(CinemachineVirtualCameraBase vcam) {
            var offset = ScreenShake.CalculateCurrentOffset();
            return vcam.transform.rotation * new Vector3(offset.x, offset.y, 0) + CameraOffset.CurrentOffset;
        }
    }
}