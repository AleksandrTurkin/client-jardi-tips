namespace JardiTips.Client.Features.Categories.Models;

public sealed record CreateCategoryRequest(
    string Name,
    string Description);
