namespace BMonkeModManager.Models;

public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string GitPath { get; set; } = string.Empty;
    public GroupInfo? GroupInfo { get; set; }  // Make nullable
    public string DownloadURL { get; set; } = string.Empty;
}

public class RawMod
{
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string GitPath { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string DownloadURL { get; set; } = string.Empty;
}