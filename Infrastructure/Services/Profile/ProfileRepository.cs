using Application.Features.Profile.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Profile;

internal sealed class ProfileRepository(IDbExecutor db) : IProfileRepository
{
    public async Task<GetProfileDbResult?> GetProfileAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        return await db.QuerySingleAsync<GetProfileDbResult>(
            "MyMoney.usp_Profile_GetProfile", p, ct);
    }

    public async Task<UpdateProfileDbResult> UpdateProfileAsync(
        UpdateProfileDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",        input.UserId,        DbType.Int64);
        p.Add("@FirstNameEn",   input.FirstNameEn,   DbType.String);
        p.Add("@LastNameEn",    input.LastNameEn,     DbType.String);
        p.Add("@FirstNameAr",   input.FirstNameAr,   DbType.String);
        p.Add("@LastNameAr",    input.LastNameAr,    DbType.String);
        p.Add("@DisplayNameEn", input.DisplayNameEn, DbType.String);
        p.Add("@DisplayNameAr", input.DisplayNameAr, DbType.String);
        p.Add("@DateOfBirth",   input.DateOfBirth,   DbType.DateTime2);
        p.Add("@GenderId",      input.GenderId,      DbType.Byte);

        return await db.QuerySingleAsync<UpdateProfileDbResult>(
            "MyMoney.usp_Profile_UpdateProfile", p, ct)
            ?? new UpdateProfileDbResult(1);
    }

    public async Task<UpdateProfilePictureDbResult> UpdateProfilePictureAsync(
        UpdateProfilePictureDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",         input.UserId,         DbType.Int64);
        p.Add("@ProfilePicture", input.ProfilePicture, DbType.String);

        return await db.QuerySingleAsync<UpdateProfilePictureDbResult>(
            "MyMoney.usp_Profile_UpdateProfilePicture", p, ct)
            ?? new UpdateProfilePictureDbResult(1, null);
    }

    public async Task<RemoveProfilePictureDbResult> RemoveProfilePictureAsync(
        long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        return await db.QuerySingleAsync<RemoveProfilePictureDbResult>(
            "MyMoney.usp_Profile_RemoveProfilePicture", p, ct)
            ?? new RemoveProfilePictureDbResult(1, null);
    }

    public async Task<IReadOnlyList<GetSessionsDbResult>> GetSessionsAsync(
        long userId, string? currentTokenHash, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",           userId,           DbType.Int64);
        p.Add("@CurrentTokenHash", currentTokenHash, DbType.String);

        return await db.QueryListAsync<GetSessionsDbResult>(
            "MyMoney.usp_Profile_GetSessions", p, ct);
    }

    public async Task<RevokeSessionDbResult> RevokeSessionAsync(
        RevokeSessionDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",      input.UserId,      DbType.Int64);
        p.Add("@TokenId",     input.TokenId,     DbType.Int64);
        p.Add("@RevokedByIp", input.RevokedByIp, DbType.String);

        return await db.QuerySingleAsync<RevokeSessionDbResult>(
            "MyMoney.usp_Profile_RevokeSession", p, ct)
            ?? new RevokeSessionDbResult(1);
    }

    public async Task RevokeAllOtherSessionsAsync(
        RevokeAllOtherSessionsDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",           input.UserId,           DbType.Int64);
        p.Add("@CurrentTokenHash", input.CurrentTokenHash, DbType.String);
        p.Add("@RevokedByIp",      input.RevokedByIp,      DbType.String);

        await db.ExecuteAsync("MyMoney.usp_Profile_RevokeAllOtherSessions", p, ct);
    }

    public async Task<GetProfileForEmailChangeDbResult?> GetProfileForEmailChangeAsync(
        long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        return await db.QuerySingleAsync<GetProfileForEmailChangeDbResult>(
            "MyMoney.usp_Profile_GetProfileForEmailChange", p, ct);
    }

    public async Task<bool> CheckEmailExistsAsync(string email, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@Email", email, DbType.String);

        var result = await db.ExecuteScalarAsync<bool?>(
            "MyMoney.usp_Authentication_CheckEmailExists", p, ct);

        return result ?? false;
    }

    public async Task RequestEmailChangeAsync(
        RequestEmailChangeDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId",       input.UserId,       DbType.Int64);
        p.Add("@NewEmail",     input.NewEmail,     DbType.String);
        p.Add("@TokenHash",    input.TokenHash,    DbType.String);
        p.Add("@ExpiresAtUtc", input.ExpiresAtUtc, DbType.DateTime2);
        p.Add("@CreatedByIp",  input.CreatedByIp,  DbType.String);

        await db.ExecuteAsync("MyMoney.usp_Profile_RequestEmailChange", p, ct);
    }

    public async Task<ConfirmEmailChangeDbResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeDbInput input, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@TokenHash", input.TokenHash, DbType.String);
        p.Add("@UsedByIp",  input.UsedByIp,  DbType.String);

        return await db.QuerySingleAsync<ConfirmEmailChangeDbResult>(
            "MyMoney.usp_Profile_ConfirmEmailChange", p, ct)
            ?? new ConfirmEmailChangeDbResult(1, null, null, null, null, null);
    }

    public async Task CancelEmailChangeAsync(long userId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@UserId", userId, DbType.Int64);

        await db.ExecuteAsync("MyMoney.usp_Profile_CancelEmailChange", p, ct);
    }
}
