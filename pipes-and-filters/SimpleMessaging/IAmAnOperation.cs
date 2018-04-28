namespace SimpleMessaging
{
    public interface IAmAnOperation<TIn, TOut> where TIn : IAmAMessage where TOut: IAmAMessage
    {
        TOut Execute(TIn message);
    }
}