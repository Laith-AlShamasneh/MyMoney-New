namespace Application.Features.Category.DbModels;

public class CategoryDbResult
{
    public int      CategoryId        { get; set; }
    public string   NameEn            { get; set; } = null!;
    public string   NameAr            { get; set; } = null!;
    public byte     TransactionTypeId { get; set; }
    public string?  IconFileName      { get; set; }
    public short    SortOrder         { get; set; }
}
