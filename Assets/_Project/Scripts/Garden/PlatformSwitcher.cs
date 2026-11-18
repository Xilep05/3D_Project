using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

namespace GardenWise
{
    // owner 3 - one Main scene for both platforms:
    // - no headset (editor, desktop build): the desktop Player is used, the XR Origin is switched off
    // - headset running (Quest build): the XR Origin is used, the desktop camera, body and keys are switched off,
    //   the screen canvases become panels floating in front of the player, and the UI listens to the controllers
    // The ToolBelt, Interactor and effects stay on the desktop Player object and are shared by both.
    public class PlatformSwitcher : MonoBehaviour
    {
        [Header("Desktop")]
        [SerializeField] private GameObject desktopCamera;
        [SerializeField] private Behaviour[] desktopOnly = new Behaviour[0];
        [SerializeField] private CharacterController desktopBody;

        [Header("VR")]
        [SerializeField] private GameObject xrOrigin;
        [SerializeField] private Transform xrHead;

        [Header("Screen canvases shown as floating panels in VR")]
        [SerializeField] private float panelDistance = 1.8f;
        [SerializeField] private float panelScale = 0.0011f;
        [SerializeField] private float panelHeightOffset = -0.15f;
        [Tooltip("The panels come back in front when the head turns more than this")]
        [SerializeField] private float followAngle = 40f;

        private Canvas[] panels = new Canvas[0];
        private bool vr;

        public bool IsVR
        {
            get { return vr; }
        }

        [Header("Editor test with the XR Device Simulator (no headset)")]
        [SerializeField] private bool forceVRInEditor = false;

        private void Awake()
        {
            vr = XRSettings.isDeviceActive || (Application.isEditor && forceVRInEditor);

            if (xrOrigin != null) xrOrigin.SetActive(vr);
            if (desktopCamera != null) desktopCamera.SetActive(!vr);
            if (desktopBody != null) desktopBody.enabled = !vr;
            foreach (Behaviour behaviour in desktopOnly)
            {
                if (behaviour != null) behaviour.enabled = !vr;
            }

            if (vr) SetupWorldSpaceUI();
        }

        private void LateUpdate()
        {
            if (!vr || xrHead == null) return;

            foreach (Canvas canvas in panels)
            {
                if (canvas == null) continue;
                Vector3 toPanel = canvas.transform.position - xrHead.position;
                Vector3 flatForward = Vector3.ProjectOnPlane(xrHead.forward, Vector3.up).normalized;
                if (Vector3.Angle(Vector3.ProjectOnPlane(toPanel, Vector3.up), flatForward) > followAngle || toPanel.magnitude > panelDistance * 2f)
                {
                    PlaceInFront(canvas.transform);
                }
            }
        }

        // the overlay canvases cannot be seen in a headset: they become world-space panels
        private void SetupWorldSpaceUI()
        {
            Camera eyes = xrHead != null ? xrHead.GetComponent<Camera>() : Camera.main;
            Type raycaster = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            Type xrModule = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");

            System.Collections.Generic.List<Canvas> found = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = eyes;
                RectTransform rect = (RectTransform)canvas.transform;
                rect.sizeDelta = new Vector2(1920f, 1080f);
                rect.localScale = Vector3.one * panelScale;
                PlaceInFront(rect);

                // the controller rays of the XR Interaction Toolkit can click the buttons
                if (raycaster != null && canvas.GetComponent(raycaster) == null) canvas.gameObject.AddComponent(raycaster);
                found.Add(canvas);
            }
            panels = found.ToArray();

            EventSystem events = FindFirstObjectByType<EventSystem>();
            if (events != null && xrModule != null && events.GetComponent(xrModule) == null)
            {
                StandaloneInputModule standalone = events.GetComponent<StandaloneInputModule>();
                if (standalone != null) standalone.enabled = false;
                events.gameObject.AddComponent(xrModule);
            }
        }

        private void PlaceInFront(Transform panel)
        {
            if (xrHead == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(xrHead.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            panel.position = xrHead.position + forward * panelDistance + Vector3.up * panelHeightOffset;
            panel.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
