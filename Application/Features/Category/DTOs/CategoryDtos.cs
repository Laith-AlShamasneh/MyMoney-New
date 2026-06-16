namespace Application.Features.Category.DTOs;

public record GetCategoriesRequest(int? TypeId);

public record CategoryResponse(
    int    CategoryId,
    string NameEn,
    string NameAr,
    int    TransactionTypeId,
    string? IconUrl,
    short  SortOrder);
