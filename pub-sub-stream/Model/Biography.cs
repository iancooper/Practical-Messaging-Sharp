using SimpleEventing;

namespace Model;

public class Biography : IAmAMessage
{
    public Biography(string id, string description)
    {
        Id = id;
        Description = description;
    }

    public string Description { get; set; }
    public string Id { get; set; }
}