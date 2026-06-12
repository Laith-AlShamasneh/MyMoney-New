using Domain.Common;
using Shared.Enums.Finance;

namespace Domain.Entities.Finance;

public class Transaction : BaseEntity
{
    public long             TransactionId       { get; set; }
    public long             UserId              { get; set; }
    public int              CategoryId          { get; set; }
    public TransactionTypes TransactionTypeId   { get; set; }
    public decimal          Amount              { get; set; }
    public string?          Description         { get; set; }
    public DateOnly         TransactionDate     { get; set; }
    public string?          Notes               { get; set; }
}
