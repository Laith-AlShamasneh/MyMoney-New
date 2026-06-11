using Shared.Enums.System;
using Shared.Responses;
using Shared.Results;

namespace WebApi.Common.Extensions;

public static class ServiceResultExtensions
{
    public static IResult ToHttpResponse<T>(this ServiceResult<T> result)
    {
        var httpStatus = MapHttpStatus(result.Code);

        var response = result.IsSuccess
            ? ApiResponse<T>.SuccessResponse(result.Data, (int)result.Code, result.Message)
            : ApiResponse<T>.Fail((int)result.Code, result.Message);

        return Results.Json(response, statusCode: httpStatus);
    }

    private static int MapHttpStatus(InternalResponseCodes code) => code switch
    {
        InternalResponseCodes.OK                 => StatusCodes.Status200OK,
        InternalResponseCodes.Created            => StatusCodes.Status200OK,
        InternalResponseCodes.Accepted           => StatusCodes.Status200OK,
        InternalResponseCodes.Found              => StatusCodes.Status200OK,
        InternalResponseCodes.BadRequest         => StatusCodes.Status200OK,
        InternalResponseCodes.Unauthorized       => StatusCodes.Status200OK,
        InternalResponseCodes.Forbidden          => StatusCodes.Status200OK,
        InternalResponseCodes.NotFound           => StatusCodes.Status200OK,
        InternalResponseCodes.Conflict           => StatusCodes.Status200OK,
        InternalResponseCodes.RequestTimeout     => StatusCodes.Status200OK,
        InternalResponseCodes.InternalServerError => StatusCodes.Status500InternalServerError,
        _                                        => StatusCodes.Status500InternalServerError
    };
}
