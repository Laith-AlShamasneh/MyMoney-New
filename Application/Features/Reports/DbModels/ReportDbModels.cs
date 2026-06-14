namespace Application.Features.Reports.DbModels;

public sealed class ReportTypeDbModel
{
    public byte   Id            { get; set; }
    public string Key           { get; set; } = null!;
    public string NameEn        { get; set; } = null!;
    public string NameAr        { get; set; } = null!;
    public string DescriptionEn { get; set; } = null!;
    public string DescriptionAr { get; set; } = null!;
    public byte   SortOrder     { get; set; }
}

public sealed class ReportDbModel
{
    public long      Id                { get; set; }
    public long      UserId            { get; set; }
    public byte      ReportTypeId      { get; set; }
    public string    ReportTypeKey     { get; set; } = null!;
    public string    ReportTypeNameEn  { get; set; } = null!;
    public string    ReportTypeNameAr  { get; set; } = null!;
    public byte      Status            { get; set; }
    public string    Language          { get; set; } = null!;
    public DateOnly  DateFrom          { get; set; }
    public DateOnly  DateTo            { get; set; }
    public string?   FilePath          { get; set; }
    public long?     FileSize          { get; set; }
    public string?   ErrorMessage      { get; set; }
    public DateTime  RequestedOnUtc    { get; set; }
    public DateTime? ProcessedOnUtc    { get; set; }
    public DateTime? CompletedOnUtc    { get; set; }
    public DateTime? ExpiresOnUtc      { get; set; }
}
