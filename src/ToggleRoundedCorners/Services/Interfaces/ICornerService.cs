namespace Dawn.Apps.ToggleRoundedCorners.Services.Interfaces;

using global::Dawn.Libs.ToggleRoundedCorners.Native;

public interface ICornerService
{
    Task<SuccessResult> InitializeIfNecessary();
    Task<SuccessResult> ResetToDefault();
    Task<SuccessResult> ToggleRoundedCorners();
    Task<SuccessResult> SetSharpCorners();
    Task<SuccessResult> SetRoundedCorners();

    Task<SuccessResult> RestartDWM();
    Task<SuccessResult> ClearCache();
}