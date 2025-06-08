#nullable enable
using Dawn.Apps.ToggleRoundedCorners.Tools;

namespace Dawn.Serilog.CustomEnrichers;

using System.Diagnostics;
using System.Text.RegularExpressions;
using global::Serilog.Core;
using global::Serilog.Events;

public partial class ClassNameEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var frame = new StackTrace()
            .GetFrames().FirstOrDefault(x =>
            {
                var type = x.GetMethod()?.ReflectedType;

                if (type == null)
                {
                    var decType = DiagnosticMethodInfo.Create(x)?.DeclaringTypeName;
                    if (decType == null || decType == typeof(ClassNameEnricher).FullName)
                        return false;

                    return !decType.StartsWith("Serilog.");
                }
            
                if (type == typeof(ClassNameEnricher))
                    return false;

                return !type.FullName!.StartsWith("Serilog.") && !type.FullName.Contains(nameof(GUISink));
            });
        if (frame == null)
            return;
        
        var decTypeName = DiagnosticMethodInfo.Create(frame)?.DeclaringTypeName;

        if (decTypeName == null)
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Source", GetClassName(decTypeName)));
    }
    
    private string? GetClassName(string type)
    {
        var last = type.Split('.').LastOrDefault();

        var str = last?.Split('+').FirstOrDefault()?.Replace("`1", string.Empty).Replace('_', '-');
        
        if (str == null)
            return null;
        
        // str = CapitalLetterReplacement().Replace(str, "$1 $2");

        return str;
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex CapitalLetterReplacement();
}