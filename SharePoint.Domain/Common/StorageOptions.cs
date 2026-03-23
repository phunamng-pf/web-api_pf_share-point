namespace SharePoint.Domain.Common;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string RootPath { get; set; } = "storage";
}
