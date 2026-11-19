using Dawn.Common;

namespace Dawn.Apps.ToggleRoundedCorners.Services.Interfaces;

using global::Dawn.Libs.ToggleRoundedCorners.Native;

public interface ICornerService
{
    Task<Error?> InitializeIfNecessary();
    Task<Error?> ResetToDefault();
    Task<Error?> ToggleRoundedCorners();
    Task<Error?> SetSharpCorners();
    Task<Error?> SetRoundedCorners();

    Task<Error?> RestartDWM();
    Task<Error?> ClearCache();
}