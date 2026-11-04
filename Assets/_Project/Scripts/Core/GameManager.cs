using UnityEngine;

namespace GardenWise
{
    // owner 1 - singleton that runs the game loop: Menu -> LevelIntro -> Playing (day after day) -> Results
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene references (can stay empty in a test scene)")]
        [SerializeField] private GardenGrid garden;
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private WaterManager water;

        [Header("Test")]
        [SerializeField] private LevelData testLevel;
        [SerializeField] private bool startTestLevelOnPlay = false;

        public GameState State { get; private set; }
        public LevelData CurrentLevel { get; private set; }
        public int CurrentDay { get; private set; }

        public bool IsPlaying
        {
            get { return State == GameState.Playing; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (startTestLevelOnPlay && testLevel != null) StartLevel(testLevel);
            else SetState(GameState.Menu);
        }

        public void StartLevel(LevelData level)
        {
            CurrentLevel = level;
            CurrentDay = 1;

            if (weather != null) weather.SetupLevel(level);
            if (water != null) water.SetupLevel(level);

            SetState(GameState.LevelIntro);
        }

        // called by the "Start" button of the intro panel
        public void BeginFirstDay()
        {
            SetState(GameState.Playing);
            GameEvents.RaiseDayStarted(CurrentDay);
        }

        // called by the bell: the night is always simulated in the same order
        public void EndDay()
        {
            if (!IsPlaying) return;

            if (garden != null && weather != null)
            {
                foreach (SoilTile tile in garden.Tiles)
                {
                    weather.ApplyNight(tile);
                    if (tile.CurrentPlant != null) tile.CurrentPlant.GrowOneDay(tile.Moisture);
                }
            }

            if (water != null && weather != null) water.ApplyRain(weather.Today);

            if (CurrentDay >= CurrentLevel.numberOfDays)
            {
                SetState(GameState.Results);
                return;
            }

            CurrentDay++;
            if (weather != null) weather.NextDay(CurrentDay);
            GameEvents.RaiseDayStarted(CurrentDay);
        }

        public void BackToMenu()
        {
            SetState(GameState.Menu);
        }

        private void SetState(GameState newState)
        {
            State = newState;
            Debug.Log("Game state: " + newState);
            GameEvents.RaiseGameStateChanged(newState);
        }
    }
}
