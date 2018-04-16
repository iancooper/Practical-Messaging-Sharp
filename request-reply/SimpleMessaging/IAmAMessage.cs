using System;

namespace SimpleMessaging
{
    public interface IAmAMessage
    {
        Guid CorrelationId { get; set; }
        string ReplyTo { get; set; } 
    }

    public interface IAmAResponse
    {
        Guid CorrelationId { get; set; }
    }
}