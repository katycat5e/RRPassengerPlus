using Track.Data;

namespace PassengerPlus
{
    public class StationAgentDTO : MapObjectDTO
    {
        public static readonly string StationAgentKind = "StationAgent";
        public override string Kind => StationAgentKind;

        public string AreaId { get; set; }

        public string PassengerStopId { get; set; }
    }
}