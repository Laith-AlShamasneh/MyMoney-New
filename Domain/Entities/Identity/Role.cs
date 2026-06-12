using Domain.Common;

namespace Domain.Entities.Identity;

public class Role : BaseEntity
{
    public int     RoleId       { get; set; }
    public string  NameEn       { get; set; } = null!;
    public string  NameAr       { get; set; } = null!;
    public string? Description  { get; set; }
    public bool    IsSystemRole { get; set; }
}
