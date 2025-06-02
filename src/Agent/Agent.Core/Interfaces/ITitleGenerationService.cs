namespace Agent.Core.Interfaces;
public interface ITitleGenerationService
{
    Task<string> GenerateTitleAsync(string message);
    Task GenerateTitleAndUpdateThreadAsync(Guid threadId, string message);
}
