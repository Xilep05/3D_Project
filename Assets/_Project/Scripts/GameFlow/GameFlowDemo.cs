using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenWise
{
    // Owner 1: test controls only. The shared GameManager remains the source of truth.
    public class GameFlowDemo : MonoBehaviour
    {
        [Header("Existing game loop and two level assets")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private LevelData firstLevel;
        [SerializeField] private LevelData secondLevel;

        [Header("Demo display")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private Button firstLevelButton;
        [SerializeField] private Button secondLevelButton;
        [SerializeField] private Button beginButton;
        [SerializeField] private Button endDayButton;
        [SerializeField] private Button backButton;

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleStateChanged;
            GameEvents.DayStarted += HandleDayStarted;
            Refresh();
        }

        private void Start() { Refresh(); }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleStateChanged;
            GameEvents.DayStarted -= HandleDayStarted;
        }

        public void SelectFirstLevel() { SelectLevel(firstLevel); }
        public void SelectSecondLevel() { SelectLevel(secondLevel); }

        private void SelectLevel(LevelData level)
        {
            if (gameManager == null || gameManager.State != GameState.Menu) return;
            if (level == null || level.numberOfDays < 1)
            {
                Debug.LogError("GameFlow demo: assign a level with at least one day.", this);
                return;
            }
            gameManager.StartLevel(level);
        }

        public void BeginLevel()
        {
            if (gameManager != null && gameManager.State == GameState.LevelIntro && gameManager.CurrentLevel != null)
                gameManager.BeginFirstDay();
        }

        public void EndDay()
        {
            if (gameManager != null && gameManager.IsPlaying && gameManager.CurrentLevel != null)
                gameManager.EndDay();
        }

        public void BackToMenu()
        {
            if (gameManager != null && gameManager.State != GameState.Menu)
                gameManager.BackToMenu();
        }

        private void HandleStateChanged(GameState state) { Refresh(); }
        private void HandleDayStarted(int day) { Refresh(); }

        private void Refresh()
        {
            bool available = gameManager != null;
            GameState state = available ? gameManager.State : GameState.Menu;
            bool menu = available && state == GameState.Menu;
            SetEnabled(firstLevelButton, menu && firstLevel != null && firstLevel.numberOfDays > 0);
            SetEnabled(secondLevelButton, menu && secondLevel != null && secondLevel.numberOfDays > 0);
            SetEnabled(beginButton, available && state == GameState.LevelIntro && gameManager.CurrentLevel != null);
            SetEnabled(endDayButton, available && state == GameState.Playing && gameManager.CurrentLevel != null);
            SetEnabled(backButton, available && state != GameState.Menu);

            if (!available)
            {
                if (statusText != null) statusText.text = "GameManager non assigné";
                if (instructionText != null) instructionText.text = "Relier le GameManager dans l'Inspector.";
                return;
            }

            LevelData level = gameManager.CurrentLevel;
            string details = state != GameState.Menu && level != null
                ? "\n" + level.levelName + " - Jour " + gameManager.CurrentDay + " / " + level.numberOfDays
                : "";
            if (statusText != null) statusText.text = "État : " + state + details;
            if (instructionText == null) return;
            switch (state)
            {
                case GameState.LevelIntro:
                    instructionText.text = level != null ? level.introText : "Choisir un niveau.";
                    break;
                case GameState.Playing:
                    instructionText.text = "Fin de journée avance d'un jour. Après le dernier jour : Results.";
                    break;
                case GameState.Results:
                    instructionText.text = "Cycle terminé. Retourner au menu pour rejouer ou choisir l'autre niveau.";
                    break;
                default:
                    instructionText.text = "Choisir un niveau pour commencer. Démo du déroulement du jeu.";
                    break;
            }
        }

        private static void SetEnabled(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }
    }
}
