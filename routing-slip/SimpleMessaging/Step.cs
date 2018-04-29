namespace SimpleMessaging
{
    public class Step 
    {
        public Step(int order, string routingKey)
        {
            Order = order;
            RoutingKey = routingKey;
        }

        public int Order { get; set; }
        public string RoutingKey { get; set; }
        public bool Completed { get; set; } = false;
    }
}