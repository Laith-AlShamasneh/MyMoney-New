namespace Shared.Responses;

public sealed record ApiResponse<T>
{
    public bool Success { get; init; }
    public int Code { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Result { get; init; }

    public static ApiResponse<T> SuccessResponse(
        T? result,
        int code,
        string message,
        int layout = 0)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = code,
            Message = message,
            Result = result
        };
    }

    public static ApiResponse<T> Fail(
        int code,
        string message,
        int layout = 0)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Result = default
        };
    }
}