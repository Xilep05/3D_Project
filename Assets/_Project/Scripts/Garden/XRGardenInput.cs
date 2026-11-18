using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace GardenWise
{
    // owner 3 - Meta Quest controls, the VR counterpart of DesktopInput: it calls the same ToolBelt and Interactor
    // right controller: point at a tile or the bell, trigger = use the tool, A = next tool, B = tool option (seed / barrel-tap)
    // walking and turning are done by the XR Origin of the XR Interaction Toolkit (teleport, snap turn)
    // only built-in XR APIs are used, so this script compiles with or without the XR packages
    public class XRGardenInput : MonoBehaviour
    {
        [SerializeField] private ToolBelt toolBelt;
        [SerializeField] private Interactor interactor;
        [SerializeField] private Transform rightController;
        [SerializeField] private Transform xrOrigin;

        [Header("Pointer line")]
        [SerializeField] private LineRenderer pointer;
        [SerializeField] private float pointerLength = 4f;
        [SerializeField] private Color pointerIdle = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color pointerOnTarget = new Color(1f, 0.85f, 0.2f, 0.9f);

        private bool triggerWasDown;
        private bool primaryWasDown;
        private bool secondaryWasDown;
        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            if (xrOrigin != null)
            {
                startPosition = xrOrigin.position;
                startRotation = xrOrigin.rotation;
            }
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleGameStateChanged;
            if (pointer != null) pointer.enabled = false;
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            bool playing = game == null || game.IsPlaying;
            if (!playing || rightController == null || interactor == null)
            {
                if (interactor != null) interactor.ClearAim();
                if (pointer != null) pointer.enabled = false;
                return;
            }

            Ray ray = new Ray(rightController.position, rightController.forward);
            interactor.Aim(ray);
            DrawPointer(ray);

            InputDevice hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (Pressed(hand, CommonUsages.triggerButton, ref triggerWasDown)) interactor.UseCurrentTool();
            if (Pressed(hand, CommonUsages.primaryButton, ref primaryWasDown)) toolBelt.SelectNext(1);
            if (Pressed(hand, CommonUsages.secondaryButton, ref secondaryWasDown)) toolBelt.Select(toolBelt.CurrentTool);
        }

        // true only on the frame the button goes down
        private static bool Pressed(InputDevice device, InputFeatureUsage<bool> button, ref bool wasDown)
        {
            bool down;
            if (!device.isValid || !device.TryGetFeatureValue(button, out down)) down = false;
            bool pressed = down && !wasDown;
            wasDown = down;
            return pressed;
        }

        private void DrawPointer(Ray ray)
        {
            if (pointer == null) return;

            bool onTarget = interactor.TargetTile != null || interactor.TargetBell != null;
            float length = pointerLength;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, pointerLength, ~0, QueryTriggerInteraction.Ignore)) length = hit.distance;

            pointer.enabled = true;
            pointer.positionCount = 2;
            pointer.SetPosition(0, ray.origin);
            pointer.SetPosition(1, ray.origin + ray.direction * length);
            Color color = onTarget ? pointerOnTarget : pointerIdle;
            pointer.startColor = color;
            pointer.endColor = new Color(color.r, color.g, color.b, color.a * 0.3f);
        }

        // each level starts from the same place, facing the garden (like PlayerController on desktop)
        private void HandleGameStateChanged(GameState state)
        {
            if (state != GameState.LevelIntro || xrOrigin == null) return;
            xrOrigin.SetPositionAndRotation(startPosition, startRotation);
        }

        // used by the scene builder: finds the right hand of an XR Origin (XR Interaction Toolkit starter rig)
        public static Transform FindRightController(Transform rig)
        {
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Right") && child.name.Contains("Controller")) return child;
            }
            return null;
        }
    }
}
