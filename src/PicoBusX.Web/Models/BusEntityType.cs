namespace PicoBusX.Web.Models;

public enum BusEntityType
{
    Queue,
    Topic,
    Subscription
}

public class BusEntity
{
    public BusEntityType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TopicName { get; set; }
    public string EntityPath => Type == BusEntityType.Subscription ? $"{TopicName}/subscriptions/{Name}" : Name;
}
