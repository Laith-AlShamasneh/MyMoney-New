using Shared.Enums.System;

namespace Shared.Results;

public static class ServiceResultFactory
{
    public static ServiceResult<T> Success<T>(T data, InternalResponseCodes code, string message) =>
        new() { IsSuccess = true, Code = code, Message = message, Data = data };

    public static ServiceResult<object?> Success<T>(InternalResponseCodes code, string message) =>
        new() { IsSuccess = true, Code = code, Message = message, Data = null };

    public static ServiceResult<T> Failure<T>(InternalResponseCodes code, string message) =>
        new() { IsSuccess = false, Code = code, Message = message, Data = default };
}