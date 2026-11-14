namespace BusJam
{
    public static class SlotRules
    {
        public static int DefaultUnlocked => GameConfigSO.Current.parking.defaultUnlockedSlots;
        public static int MaxSlots => GameConfigSO.Current.parking.maxSlots;
    }
}