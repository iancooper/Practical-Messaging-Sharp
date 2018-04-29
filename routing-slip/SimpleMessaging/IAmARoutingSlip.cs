using System.Collections.Generic;

namespace SimpleMessaging
{
    public interface IAmARoutingSlip : IAmAMessage
    {
        int CurrentStep { get; set; }
        SortedList<int, Step> Steps { get; }
    }
}