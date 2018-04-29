using System.Collections.Generic;
using SimpleMessaging;

namespace Model
{
    /// <summary>
    /// Whereas with the pipes-and=filter approach we could have a different message type emitted from the filter than
    /// received, with a routing slip we have to use the same message throughout the pipeline. It either has to contain
    /// all the properties are steps need (possibly nullable as we may not need them all) or a bucket to put properties
    /// that one step adds
    /// </summary>
    public class Greeting : RoutingSlip
    {
        public string Salutation { get; set; } = "Hello World";
        public string Recipient { get; set; }
    }
}