namespace Dawn.Apps.ToggleRoundedCorners.Services;

using System.Diagnostics;
using System.IO;
using Interfaces;
using Microsoft.Win32.TaskScheduler;
using Tools;

/// <summary>
/// Provides a service to manage scheduled tasks for startup and running the application with elevated privileges.
/// </summary>
internal sealed class AdminTaskScheduler(ILogger logger, TaskService scheduler) : ITaskSchedulerService
{
    public bool IsEnabled(string key)
    {
        try
        {
            if (!Contains(key))
                return false;

            var task = scheduler.GetTask(key);
            var file = new FileInfo(Application.ExecutablePath);

            if (task.Definition.Actions.FirstOrDefault(x => x.ActionType == TaskActionType.Execute) is not ExecAction execAction)
                return false;
            
            if (execAction.Path == file.FullName)
                return task.Definition.Settings.Enabled;
                
            logger.Warning("Task '{TaskName}' paths differ, '{OurPath}' vs '{TaskSchedulerPath}'", key, file.FullName, execAction.Path);
            return false;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to check if task '{TaskName}' is enabled", key);
            return false;
        }
    }

    public bool RunIfPresent(string key)
    {
        if (!IsEnabled(key))
        {
            logger.Warning("Task '{TaskName}' is not present", key);
            return false;
        }

        Run(key);
        logger.Verbose("Task '{TaskName}' is present, elevating...", key);
        return true;
    }

    public bool Contains(string key)
    {
        try
        {
            using var task = scheduler.GetTask(key);

            if (task?.Definition.Actions.FirstOrDefault() is not ExecAction)
                return false;

            // Now we check if the task is ran as admin
            var principal = task.Definition.Principal;

            return principal.RunLevel == TaskRunLevel.Highest;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to check if task '{TaskName}' is present", key);
            return false;
        }

    }

    public void Run(string key)
    {
        try
        {
            scheduler.GetTask(key)?.Run();
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to run task '{TaskName}'", key);
        }
    }

    public void RunOnStartup(string key, params string[] args)
    {
        try
        {
            if (Contains(key))
            {
                logger.Warning("Task '{TaskName}' is already present, removing", key);
                Remove(key);
            }

            var file = new FileInfo(Application.ExecutablePath);

            if (!file.Exists)
                throw new FileNotFoundException(file.FullName);

            logger.Verbose("Creating new task under the name of '{TaskName}'", key);
            using var td = scheduler.NewTask();

            RunAsAdmin(file, args, td);
            OnStartup(td);

            td.Settings.Priority = ProcessPriorityClass.High;

            CloseAfter(TimeSpan.FromMinutes(5), td);
            FixSchedulerBugIfNecessary(td);

            SaveTaskDefinition(key, td);
            logger.Information("Task '{TaskName}' is created", key);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to create task '{TaskName}'", key);
        }

    }

    public bool AddIfEnabled(string key, params string[] args)
    {
        try
        {
            if (Contains(key) && !IsEnabled(key))
            {
                return TryRefresh(key);
                // logger.Warning("Task '{TaskName}' is already present, removing", key);
                // Remove(key);
            }

            var file = new FileInfo(Application.ExecutablePath);

            if (!file.Exists)
                throw new FileNotFoundException(file.FullName);

            logger.Verbose("Creating new task under the name of '{TaskName}'", key);
            using var td = scheduler.NewTask();

            RunAsAdmin(file, args, td);
            
            AutoDeleteIfUnusedAfter(TimeSpan.FromDays(15), td, key);
            FixSchedulerBugIfNecessary(td);

            SaveTaskDefinition(key, td);
            logger.Information("Task '{TaskName}' is created", key);
            return true;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to create task '{TaskName}'", key);
            return false;
        }
    }

    private void CloseAfter(TimeSpan fromMinutes, TaskDefinition taskDefinition) => taskDefinition.Settings.ExecutionTimeLimit = fromMinutes;

    private void OnStartup(TaskDefinition taskDefinition)
    {
        taskDefinition.Triggers.AddRange(
        [
            new LogonTrigger(),
            
            // "The system has resumed from sleep."
            new EventTrigger("System", "Microsoft-Windows-Kernel-Power", 107),
            
            // "The Desktop Window Manager has registered the session port." (DWM Started / Restarted, usually happens if DWM exits for any reason)
            new EventTrigger("Application", "Desktop Window Manager", 9027),
        ]);
    }

    private void SaveTaskDefinition(string key, TaskDefinition taskDefinition)
    {
        scheduler.RootFolder.RegisterTaskDefinition(key, taskDefinition);
        using var task = scheduler.GetTask(key);
        task.Enabled = true;
        task.RegisterChanges();
    }

    private static void RunAsAdmin(FileInfo file, string[] args, TaskDefinition taskDefinition)
    {
        taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
        taskDefinition.Settings.AllowDemandStart = true;
        taskDefinition.Actions.Add(new ExecAction(file.FullName, string.Join(' ', args)));
        taskDefinition.Settings.StopIfGoingOnBatteries = false;
        taskDefinition.Settings.DisallowStartIfOnBatteries = false;
    }

    private void FixSchedulerBugIfNecessary(TaskDefinition td)
    {
        if (td.Principal.UserId == Environment.UserName)
            td.Principal.UserId = td.Principal.Account;
    }

    public bool Remove(string key)
    {
        try
        {
            if (!Contains(key))
            {
                logger.Warning("Task '{TaskName}' is not present", key);
                return false;
            }

            scheduler.RootFolder.DeleteTask(key, false);
            logger.Information("Task '{TaskName}' is removed", key);
            return true;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to remove task '{TaskName}'", key);
            return false;
        }

    }


    // This attaches a stub event trigger with no intention of being triggered, but an expiry is attached which can be modified to prevent the task expiring
    private void AutoDeleteIfUnusedAfter(TimeSpan time, TaskDefinition td, string key)
    {
        // This is a fake event trigger that isn't meant to be triggered but rather to have an expiry attached
        // The expiry will be refreshed if the app starts
        if (td.Triggers.Count == 0) 
            td.Triggers.Add(new EventTrigger("Application", "NONE", 9999));
        
        foreach (var trigger in td.Triggers) 
            trigger.EndBoundary = DateTime.Today + time;

        if (td.Actions.FirstOrDefault(x => x.ActionType == TaskActionType.Execute) is ExecAction execAction) 
            execAction.Arguments += $" --scheduler-refresh-time={time.TotalSeconds}";
        
        td.Settings.DeleteExpiredTaskAfter = TimeSpan.FromSeconds(1);
        
        logger.Verbose("Task '{TaskName}' is set to auto-delete after {Time}", key, time);
    }
    
    // This attempts to reset the expiry date of the task. Auto-Deletion occurs when the triggers have all expired
    public bool TryRefresh(string key)
    {
        try
        {
            var task = scheduler.GetTask(key);

            if (task is null)
            {
                logger.Warning("Task '{TaskName}' is not present, can't refresh", key);
                return false;
            }

            var td = task.Definition;

            if (td.Actions.FirstOrDefault(x => x.ActionType == TaskActionType.Execute) is not ExecAction execAction)
            {
                logger.Warning("Task '{TaskName}' has no action associated", key);
                return false;
            }

            var args = execAction.Arguments.Split(' ');
            var refreshValue = LaunchArgs.ExtractArgumentValue("--scheduler-refresh-time=", args);

            if (string.IsNullOrWhiteSpace(refreshValue) || !long.TryParse(refreshValue, out var refreshSeconds))
            {
                logger.Warning("Task '{TaskName}' does not have a valid refresh value associated, won't refresh", key);
                return false;
            }

            if (td.Triggers.Count == 0)
            {
                logger.Warning("Task '{TaskName}' does not have any triggers, adding dummy trigger instead", key);
                td.Triggers.Add(new EventTrigger("Application", "NONE", 9999));
            }
            
            var time = TimeSpan.FromSeconds(refreshSeconds);
            var expiryDate = DateTime.Today + time;
            
            foreach (var trigger in td.Triggers) 
                trigger.EndBoundary = expiryDate;

            task.RegisterChanges();
            logger.Verbose("Task '{TaskName}' is refreshed, new expiry date is {ExpiryDate}", key, expiryDate);
            return true;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to refresh task '{TaskName}'", key);
            return false;
        }
    }
}