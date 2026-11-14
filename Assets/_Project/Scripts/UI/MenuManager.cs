using TMPro;
using UnityEngine;

namespace GardenWise
{
    // owner 6 - shows the right panel for each game state and handles the menu buttons
    public class MenuManager : MonoBehaviour
    {
        [Header("Panels (only one is visible at a time)")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject introPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private ResultPanel resultPanel;

        [Header("Level select (same order in both lists)")]
        [SerializeField] private LevelData[] levels;
        [SerializeField] private LevelButton[] levelButtons;

        [Header("Intro")]
        [SerializeField] private TMP_Text introTitle;
        [SerializeField] private TMP_Text introText;

        private int currentLevelIndex;

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleGameStateChanged;
        }

        // one panel per state of the GameManager
        private void HandleGameStateChanged(GameState state)
        {
            HideAllPanels();

            switch (state)
            {
                case GameState.Menu:
                    mainMenuPanel.SetActive(true);
                    break;
                case GameState.LevelIntro:
                    ShowIntro();
                    break;
                case GameState.Playing:
                    hudPanel.SetActive(true);
                    break;
                case GameState.Results:
                    resultPanel.Show(currentLevelIndex);
                    break;
            }
        }

        private void HideAllPanels()
        {
            mainMenuPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            introPanel.SetActive(false);
            hudPanel.SetActive(false);
            resultPanel.gameObject.SetActive(false);
        }

        private void ShowIntro()
        {
            LevelData level = GameManager.Instance.CurrentLevel;
            introTitle.text = level.levelName;
            introText.text = level.introText;
            introPanel.SetActive(true);
        }

        // reads the save file to show the stars and to lock the levels not reached yet
        private void ShowLevelSelect()
        {
            SaveData save = SaveSystem.Load();

            for (int i = 0; i < levelButtons.Length; i++)
            {
                bool hasLevel = i < levels.Length && levels[i] != null;
                bool unlocked = i == 0 || save.starsPerLevel[i - 1] > 0;

                string title = hasLevel ? levels[i].levelName : "Coming soon";
                if (hasLevel && !unlocked) title += "\n(locked)";

                string best = "";
                if (save.starsPerLevel[i] > 0)
                {
                    best = "Best: harvest " + Mathf.RoundToInt(save.bestHarvestPerLevel[i]) + " %, water " +
                           Mathf.RoundToInt(save.bestWaterPerLevel[i]) + " %";
                }

                levelButtons[i].Show(title, save.starsPerLevel[i], best, hasLevel && unlocked);
            }

            mainMenuPanel.SetActive(false);
            levelSelectPanel.SetActive(true);
        }

        // ---- buttons (linked in the Inspector, On Click) ----

        public void OnPlayClicked()
        {
            ShowLevelSelect();
        }

        public void OnQuitClicked()
        {
            Debug.Log("Quit (only closes the game in a build)");
            Application.Quit();
        }

        public void OnBackClicked()
        {
            levelSelectPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }

        // index = 0 for level 1, 1 for level 2...
        public void OnLevelClicked(int index)
        {
            currentLevelIndex = index;
            GameManager.Instance.StartLevel(levels[index]);
        }

        public void OnStartClicked()
        {
            GameManager.Instance.BeginFirstDay();
        }

        // back to the level select, so the player sees the new stars
        public void OnContinueClicked()
        {
            GameManager.Instance.BackToMenu();
            ShowLevelSelect();
        }
    }
}
