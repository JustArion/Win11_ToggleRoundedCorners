namespace Dawn.Libs.ToggleRoundedCorners.Native;

public class ProgressAwareObject
{
    public event Action<string>? AlertProgress;

    protected void InformProgress(string str)
    {
        AlertProgress?.Invoke(str);
    }
    
    protected virtual void OnProgress(string str) {}
}