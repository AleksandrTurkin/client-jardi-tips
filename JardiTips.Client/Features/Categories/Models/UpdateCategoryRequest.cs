namespace JardiTips.Client.Features.Categories.Models;

public sealed record UpdateCategoryRequest(
    string Name,
    string Description);
