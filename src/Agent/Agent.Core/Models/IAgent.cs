namespace Agent.Core.Models
{
    public interface IAgent
    {
        string Name { get; }
        Task<string> Ask(string question);
    }
}
