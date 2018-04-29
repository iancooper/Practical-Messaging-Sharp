namespace SimpleMessaging
{
    public interface IAmAnOperation<T> where T : IAmARoutingSlip
    {
        T Execute(T message);
    }
}