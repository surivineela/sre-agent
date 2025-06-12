namespace Agent.Runtime.Interfaces;
public interface IIncidentScanner
{
    /// <summary>
    /// Scanning for incidents as a background task.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task ScanAsync(CancellationToken cancellationToken);
}
