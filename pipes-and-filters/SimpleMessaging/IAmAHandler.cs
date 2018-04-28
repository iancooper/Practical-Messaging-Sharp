namespace SimpleMessaging
{
    public interface IAmAHandler<T> where T: IAmAMessage
    {
        void Handle(T message);
    }
}