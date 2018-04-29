using System.Collections.Generic;

namespace SimpleMessaging
{
    public class RoutingSlip : IAmARoutingSlip
    {
        private readonly SortedList<int, Step>_steps = new SortedList<int, Step>();
        
        public int CurrentStep { get; set; } = 1;
        public SortedList<int, Step> Steps => _steps;
   }
}