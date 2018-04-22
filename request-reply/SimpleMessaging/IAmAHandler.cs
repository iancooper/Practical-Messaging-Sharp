namespace SimpleMessaging
{
    public interface IAmAHandler<T, TResponse> where T: IAmAMessage where TResponse: IAmAResponse
    {
        TResponse Handle(T message);
    }
}