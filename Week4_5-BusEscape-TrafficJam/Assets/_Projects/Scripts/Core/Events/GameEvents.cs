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
        public LevelLoadedEvent(int level) { Level = level; }
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
        public SlotsChangedEvent(int unlocked) { Unlocked = unlocked; }
    }

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
    public readonly struct RestartRequestedEvent { }
    public readonly struct NextLevelRequestedEvent { }
}