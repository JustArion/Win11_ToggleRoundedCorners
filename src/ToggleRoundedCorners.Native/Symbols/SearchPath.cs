namespace Dawn.Libs.ToggleRoundedCorners.Native.Symbols;

using System.Text;

internal static class SearchPath
{
    internal static string Create(string symbolServer, FileInfo? cacheLocation = null)
    {
        var sb = new StringBuilder();
        
        var cache = cacheLocation == null 
            ? string.Empty 
            : cacheLocation.FullName;
        
        sb.Append($"cache*{cache};");
        sb.Append($"srv*{symbolServer}");

        return sb.ToString();
    }

    internal static string CreateDefault() => Create("https://msdl.microsoft.com/download/symbols");
}