using System.Collections.ObjectModel;

namespace AMonkeModManager.Models;

public class GroupInfo
{
    public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    public ObservableCollection<ModInfo> Mods { get; set; } = new();
}
