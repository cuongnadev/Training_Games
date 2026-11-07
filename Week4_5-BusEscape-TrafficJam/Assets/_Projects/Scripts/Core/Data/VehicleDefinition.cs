namespace BusJam
{
    public class VehicleDefinition
    {
        public int Length { get; }
        public int Capacity { get; }

        public VehicleDefinition(int length, int capacity)
        {
            Length = length;
            Capacity = capacity;
        }
    }
}