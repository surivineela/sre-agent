using Agent.Data.DataModels;

namespace Agent.Data.Repositories;

public interface IIncidentRepository
{

    /// <summary>
    /// List all PagerDuty incidents
    Task<List<PagerDutyIncidentDocument>> GetAllPagerDutyIncidentsAsync();

    /// <summary>
    /// List all AzMon incidents
    Task<List<AzMonitorAlertDocument>> GetAllAzMonIncidentsAsync();
}
