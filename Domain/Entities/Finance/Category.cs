using Domain.Common;
using Shared.Enums.Finance;

namespace Domain.Entities.Finance;

public class Category : BaseEntity
{
    public int              CategoryId          { get; set; }
    public string           NameEn                { get; set; } = null!;
    public string           NameAr              { get; set; } = null!;
    public TransactionTypes TransactionTypeId   { get; set; }
    public string?          IconFileName        { get; set; }
    public short            SortOrder           { get; set; }
}
