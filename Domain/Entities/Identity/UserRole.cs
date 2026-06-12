namespace Domain.Entities.Identity;

// Junction table — no BaseEntity (no UpdatedAt/UpdatedBy; rows are inserted or deleted only)
public class UserRole
{
    public long      UserId    { get; set; }
    public int       RoleId    { get; set; }
    public DateTime  CreatedAt { get; set; }
    public long?     CreatedBy { get; set; }
}
