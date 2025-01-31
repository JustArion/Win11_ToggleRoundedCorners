namespace Dawn.Apps.ToggleRoundedCorners.Services.Interfaces;

public interface ITaskSchedulerService
{
    bool IsEnabled(string key);

    bool RunIfPresent(string key);
    bool Contains(string key);
    void Run(string key);
    void RunOnStartup(string key, params string[] args);

    bool AddIfEnabled(string key, params string[] args);
    bool Remove(string key);

    /// <summary>
    /// Refresh an manual event's expiry
    /// </summary>
    /// <param name="key"></param>
    bool TryRefresh(string key);
}