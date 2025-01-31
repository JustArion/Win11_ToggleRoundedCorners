namespace Dawn.Libs.ToggleRoundedCorners.Native;

using System.Diagnostics.CodeAnalysis;

public readonly struct SuccessResult<T>
{
    public bool Success { get; init; }
    
    [MemberNotNullWhen(true, nameof(Success))]
    public T? Value { get; init; }
    
    [MemberNotNullWhen(false, nameof(Success))]
    public Exception? Exception { get; init; }
    
    public static implicit operator bool(SuccessResult<T> result) => result.Success;
    
    public static implicit operator T?(SuccessResult<T> result) => result.Value;
    
    public static implicit operator SuccessResult<T>(T? value) => new() { Success = true, Value = value };
    
    public void Deconstruct(out bool success, out Exception? exception, out T? value)
    {
        success = Success;
        exception = Exception;
        value = Value;
    }
    
    public static SuccessResult<T> Failed => new() { Success = false };
}

public readonly struct SuccessResult
{
    public bool Success { get; init; }
    
    [MemberNotNullWhen(false, nameof(Success))]
    public Exception? Exception { get; init; }
    
    public static implicit operator bool(SuccessResult result) => result.Success;
    
    public static implicit operator SuccessResult(bool value) => new() { Success = value };
    
    public void Deconstruct(out bool success)
    {
        success = Success;
    }
    
    public static SuccessResult Failed => new() { Success = false };
}