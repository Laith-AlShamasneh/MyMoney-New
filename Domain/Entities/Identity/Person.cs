using Domain.Common;
using Shared.Enums.Identity;

namespace Domain.Entities.Identity;

public class Person : BaseEntity
{
    public long     PersonId       { get; set; }
    public string   FirstNameEn    { get; set; } = null!;
    public string   LastNameEn     { get; set; } = null!;
    public string?  FirstNameAr    { get; set; }
    public string?  LastNameAr     { get; set; }
    public string   DisplayNameEn  { get; set; } = null!;
    public string?  DisplayNameAr  { get; set; }
    public DateOnly? DateOfBirth   { get; set; }
    public GenderTypes? GenderId   { get; set; }
    public string?  ProfilePicture { get; set; }
}
