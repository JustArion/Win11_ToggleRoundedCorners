namespace Dawn.Apps.ToggleRoundedCorners.Tools;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("ReSharper", "InvertIf")]
public struct LaunchArgs
{

    public const string USE_SHARP_CORNERS_ARGUMENT = "--sharp-corners";
    public const string NO_FILE_LOGGING_ARGUMENT = "--no-file-logging";
    public const string EXTENDED_LOGGING_ARGUMENT = "--extended-logging";
    public const string CUSTOM_SEQ_URL_ARGUMENT = "--seq-url=";
    public const string BIND_TO_ARGUMENT = "--bind-to=";
    public const string HEADLESS_ARGUMENT = "--headless";
    
    public LaunchArgs(string[] args)
    {
        RawArgs = args;
        CommandLine = string.Join(" ", args);
        UseSharpCorners = args.Contains(USE_SHARP_CORNERS_ARGUMENT);
        
        ExtendedLogging  = args.Contains(EXTENDED_LOGGING_ARGUMENT);
        NoFileLogging = args.Contains(NO_FILE_LOGGING_ARGUMENT);
        
        CustomSeqUrl = ExtractArgumentValue(CUSTOM_SEQ_URL_ARGUMENT, args);
        HasCustomSeqUrl = Uri.TryCreate(CustomSeqUrl, UriKind.Absolute, out _);
        Headless = args.Contains(HEADLESS_ARGUMENT);

        if (int.TryParse(ExtractArgumentValue(BIND_TO_ARGUMENT, args), out var pid))
        {
            ProcessBinding = pid;
            HasProcessBinding = true;
        }
    }
    
    public IReadOnlyList<string> RawArgs { get; }
    public string CommandLine { get; }
    
    public bool UseSharpCorners { get; }
    
    public bool NoFileLogging { get; }
    public bool ExtendedLogging { get; }
    
    public bool HasCustomSeqUrl { get; }
    public string CustomSeqUrl { get; }

    public bool HasProcessBinding { get; }
    public int ProcessBinding { get; }
    public bool Headless { get; set; }
    
    public static string ExtractArgumentValue(string argumentKey, IEnumerable<string> args)
    {
        var rawrArgument = args.FirstOrDefault(x => x.StartsWith(argumentKey));

        if (string.IsNullOrWhiteSpace(rawrArgument))
            return string.Empty;

        var keyValue = rawrArgument.Split('=');

        return keyValue.Length > 1 ? keyValue[1] : string.Empty;
    }
}