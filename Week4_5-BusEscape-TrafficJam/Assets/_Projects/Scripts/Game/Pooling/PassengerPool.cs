namespace BusJam
{
    public sealed class PassengerPool : PrefabPool<PassengerController>
    {
        protected override void OnTaken(PassengerController p)    => p.ResetForPool();
        protected override void OnReturned(PassengerController p) => p.ResetForPool();
    }
}