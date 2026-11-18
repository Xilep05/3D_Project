using UnityEngine;

namespace GardenWise
{
    // owner 3 - first-person body on desktop: walks and looks around, nothing else
    // it does not read any key: DesktopInput calls Move and Look. In VR the XR Origin replaces this object
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;

        public Transform CameraPivot
        {
            get { return cameraPivot; }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // start with the pitch set in the scene (the camera looks a bit down at the garden)
            if (cameraPivot != null)
            {
                pitch = cameraPivot.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
            }
        }

        // input: x = right / left, y = forward / back, between -1 and 1. Call it every frame (gravity)
        public void Move(Vector2 input)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 direction = transform.right * input.x + transform.forward * input.y;

            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            verticalSpeed += gravity * Time.deltaTime;

            Vector3 velocity = direction * walkSpeed + Vector3.up * verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        // delta: mouse movement of this frame
        public void Look(Vector2 delta)
        {
            transform.Rotate(0f, delta.x * lookSensitivity, 0f);

            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, minPitch, maxPitch);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
