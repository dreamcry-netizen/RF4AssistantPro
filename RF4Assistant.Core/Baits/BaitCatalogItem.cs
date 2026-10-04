using System.Text.Json.Serialization;

namespace RF4AssistantPro.Baits;

public sealed class BaitCatalogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public string Brand { get; set; } = "";

    public string Category { get; set; } = "";

    public string Subcategory { get; set; } = "";

    public string Aliases { get; set; } = "";

    public string ImagePath { get; set; } = "";

    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public string DisplayName => string.Join(
        " ",
        new[] { Brand, Name }.Where(value => !string.IsNullOrWhiteSpace(value)));

    [JsonIgnore]
    public string CategoryDisplay => string.IsNullOrWhiteSpace(Subcategory)
        ? Category
        : $"{Category} / {Subcategory}";
}