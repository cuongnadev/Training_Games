namespace BusJam
{
    public readonly struct GameStateChangedEvent
    {
        public readonly GameState State;
        public GameStateChangedEvent(GameState state) { State = state; }
    }

    public readonly struct LevelLoadedEvent
    {
        public readonly int Level;
        public readonly int RewardCoins;

        public LevelLoadedEvent(int level, int rewardCoins)
        {
            Level = level;
            RewardCoins = rewardCoins;
        }
    }

    public readonly struct PassengersChangedEvent
    {
        public readonly int Remaining;
        public PassengersChangedEvent(int remaining) { Remaining = remaining; }
    }

    public readonly struct LevelLostEvent
    {
        public readonly LoseReason Reason;
        public LevelLostEvent(LoseReason reason) { Reason = reason; }
    }

    public readonly struct SlotsChangedEvent
    {
        public readonly int Unlocked;
        public readonly int NextCost;
        public readonly bool CanUnlockMore;
        public readonly bool JustPurchased;

        public SlotsChangedEvent(int unlocked, int nextCost, bool canUnlockMore, bool justPurchased)
        {
            Unlocked = unlocked;
            NextCost = nextCost;
            CanUnlockMore = canUnlockMore;
            JustPurchased = justPurchased;
        }
    }

    public readonly struct NoFreeSlotTriggeredEvent { }

    public readonly struct SettingsChangedEvent
    {
        public readonly bool IsSoundOn;
        public readonly bool IsMusicOn;
        public readonly bool IsHapticOn;

        public SettingsChangedEvent(bool isSoundOn, bool isMusicOn, bool isHapticOn)
        {
            IsSoundOn = isSoundOn;
            IsMusicOn = isMusicOn;
            IsHapticOn = isHapticOn;
        }
    }

    public readonly struct CoinsChangedEvent
    {
        public readonly int Total;
        public readonly int Delta;

        public CoinsChangedEvent(int total, int delta)
        {
            Total = total;
            Delta = delta;
        }
    }

    public readonly struct PlayRequestedEvent { }
    public readonly struct NewGameRequestedEvent { }
    public readonly struct GoHomeRequestedEvent { }
    public readonly struct RestartRequestedEvent { }
    public readonly struct NextLevelRequestedEvent { }

    public readonly struct SelectLevelRequestedEvent
    {
        public readonly int LevelIndex;
        public SelectLevelRequestedEvent(int levelIndex) { LevelIndex = levelIndex; }
    }
}