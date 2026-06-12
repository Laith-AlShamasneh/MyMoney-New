using Domain.Common;

namespace Domain.Entities.Identity;

public class User : BaseEntity
{
    public long      UserId               { get; set; }
    public long      PersonId             { get; set; }
    public string    Email                { get; set; } = null!;
    public string    PasswordHash         { get; set; } = null!;
    public bool      IsEmailConfirmed     { get; set; }
    public bool      IsLocked             { get; set; }
    public int       FailedLoginAttempts  { get; set; }
    public DateTime? LastLoginDateUtc     { get; set; }
    public DateTime? LockoutEndDateUtc    { get; set; }
}
