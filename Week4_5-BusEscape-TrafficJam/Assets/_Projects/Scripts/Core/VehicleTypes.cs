namespace BusJam
{
    public enum VehicleKind
    {
        Car,
        Bus,
    }

    public enum VehicleSkin
    {
        Arrow,
        Seat,
    }

    public static class VehicleExtensions
    {
        public static int Length(this VehicleKind vehicleKind) => vehicleKind == VehicleKind.Car ? 2 : 3; 

        public static VehicleKind FromLength(int length) => length <= 2 ? VehicleKind.Car : VehicleKind.Bus;
    }
}
