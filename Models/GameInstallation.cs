using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace BMonkeModManager.Models;

public class GameInstallation
{
    public string Path { get; set; }
    public string OSPlatform { get; set; }
    public bool BepInExInstalled { get; set; } = false;
    public string BepInExVersion { get; set; } = string.Empty;
    public bool IsIL2CPP { get; set; } = false;

    public GameInstallation() { }

    public GameInstallation(string path)
    {
        Path = path;
        OSPlatform = MainWindow.GetOS();
        BepInExInstalled = File.Exists(System.IO.Path.Combine(path, "BepInEx", "core", "BepInEx.dll"));
        BepInExVersion = BepInExInstalled ? AssemblyName.GetAssemblyName(System.IO.Path.Combine(path, "BepInEx", "core", "BepInEx.dll")).Version?.ToString() ?? string.Empty : string.Empty;
        IsIL2CPP = Directory.Exists(System.IO.Path.Combine(path, "BepInEx", "unity-libs"));
    }
}