using UnityEngine;

namespace GardenWise
{
    // owner 3 - the ONLY script that reads the keyboard and the mouse for the player
    // ZQSD or WASD or arrows: walk / mouse: look / 1-4 or wheel: tool / left click: use / Esc: free the mouse
    // for the VR version this component is disabled and the XR rig calls the same ToolBelt and Interactor methods
    public class DesktopInput : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private ToolBelt toolBelt;
        [SerializeField] private Interactor interactor;
        [SerializeField] private Camera viewCamera;

        private bool cursorLocked;
        private bool wasPlaying;

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (toolBelt == null) toolBelt = GetComponent<ToolBelt>();
            if (interactor == null) interactor = GetComponent<Interactor>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            // in the menus and on the results screen the mouse is free and the player does not move
            bool playing = IsPlaying();
            if (!playing)
            {
                if (cursorLocked) SetCursorLocked(false);
                interactor.ClearAim();
                player.Move(Vector2.zero);
                wasPlaying = false;
                return;
            }

            if (!wasPlaying) SetCursorLocked(true);
            wasPlaying = true;

            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);

            if (!cursorLocked)
            {
                player.Move(Vector2.zero);
                interactor.ClearAim();
                // first click takes the mouse back, it does not use the tool
                if (Input.GetMouseButtonDown(0)) SetCursorLocked(true);
                return;
            }

            player.Move(ReadMove());
            player.Look(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")));

            ReadToolKeys();

            if (viewCamera != null) interactor.Aim(viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));
            if (Input.GetMouseButtonDown(0)) interactor.UseCurrentTool();
        }

        private void ReadToolKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) toolBelt.Select(GardenTool.SeedBag);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) toolBelt.Select(GardenTool.WateringCan);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) toolBelt.Select(GardenTool.Mulch);
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) toolBelt.Select(GardenTool.HarvestBasket);

            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0f) toolBelt.SelectNext(-1);
            else if (wheel < 0f) toolBelt.SelectNext(1);
        }

        // AZERTY (ZQSD) and QWERTY (WASD) keyboards both work, arrows too
        private static Vector2 ReadMove()
        {
            float x = 0f;
            float y = 0f;

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;

            return new Vector2(x, y);
        }

        private static bool IsPlaying()
        {
            GameManager game = GameManager.Instance;
            return game == null || game.IsPlaying;
        }

        private void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
