using Application.Features.Authentication.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Authentication;

internal sealed class AuthRepository(IDbExecutor db) : IAuthRepository
{
    public async Task<bool> CheckEmailExistsAsync(string email, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Email", email);
        var result = await db.ExecuteScalarAsync<bool?>("MyMoney.usp_Authentication_CheckEmailExists", p, ct);
        return result ?? false;
    }

    public async Task<RegisterDbResult?> RegisterAsync(RegisterDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@FirstNameEn",    input.FirstNameEn);
        p.Add("@LastNameEn",     input.LastNameEn);
        p.Add("@FirstNameAr",    input.FirstNameAr);
        p.Add("@LastNameAr",     input.LastNameAr);
        p.Add("@DisplayNameEn",  input.DisplayNameEn);
        p.Add("@DisplayNameAr",  input.DisplayNameAr);
        p.Add("@DateOfBirth",    input.DateOfBirth?.ToDateTime(TimeOnly.MinValue), DbType.DateTime2);
        p.Add("@GenderId",       input.GenderId.HasValue ? (byte)input.GenderId.Value : (byte?)null, DbType.Byte);
        p.Add("@ProfilePicture", input.ProfilePicture);
        p.Add("@Email",          input.Email);
        p.Add("@PasswordHash",   input.PasswordHash);
        p.Add("@DefaultRoleId",  input.DefaultRoleId);

        return await db.QuerySingleAsync<RegisterDbResult>("MyMoney.usp_Authentication_Register", p, ct);
    }

    public async Task SaveRefreshTokenAsync(SaveRefreshTokenDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",       input.UserId);
        p.Add("@Token",        input.Token);
        p.Add("@ExpiresOnUtc", input.ExpiresOnUtc, DbType.DateTime2);
        p.Add("@CreatedByIp",  input.CreatedByIp);

        await db.ExecuteAsync("MyMoney.usp_Authentication_SaveRefreshToken", p, ct);
    }
}
