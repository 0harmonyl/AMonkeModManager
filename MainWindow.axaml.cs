using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BMonkeModManager.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BMonkeModManager;

public partial class MainWindow : Window
{
    public const string GROUPS_URL = "https://raw.githubusercontent.com/The-Graze/MonkeModInfo/master/groupinfo.json";
    public const string MODS_URL = "https://raw.githubusercontent.com/The-Graze/MonkeModInfo/master/modinfo.json";

    private GameInstallation? gameInstallation;
    private List<GroupInfo> groupInfos = new();

    private ObservableCollection<GroupWithMods> groupedMods = new();
    private ObservableCollection<ModInfo> allMods = new();

    public MainWindow()
    {
        InitializeComponent();

        Browse.Click += Browse_Click;
        SearchBox.TextChanged += SearchBox_TextChanged;
        CategoryViewRadio.Checked += ViewMode_Changed;
        ListViewRadio.Checked += ViewMode_Changed;
        GridViewRadio.Checked += ViewMode_Changed;

        DetectGamePath();
        _ = InitPlugins();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        FilterMods();
    }

    private void ViewMode_Changed(object? sender, RoutedEventArgs e)
    {
        if (CategoryViewRadio.IsChecked == true)
        {
            CategoryScrollViewer.IsVisible = true;
            ListScrollViewer.IsVisible = false;
        }
        else if (ListViewRadio.IsChecked == true)
        {
            CategoryScrollViewer.IsVisible = false;
            ListScrollViewer.IsVisible = true;
        }
        // GridViewRadio will later need a separate ScrollViewer if you implement a grid view
    }

    private void FilterMods()
    {
        var searchText = SearchBox?.Text?.ToLower() ?? "";

        if (string.IsNullOrWhiteSpace(searchText))
        {
            CategoryTreeView.ItemsSource = groupedMods;
            ModsList.ItemsSource = allMods;
        }
        else
        {
            var filtered = allMods.Where(m =>
                m.Name.ToLower().Contains(searchText) ||
                m.Author.ToLower().Contains(searchText) ||
                (m.GroupInfo?.Name?.ToLower().Contains(searchText) ?? false)
            ).ToList();

            ModsList.ItemsSource = filtered;

            var filteredGroups = groupedMods.Where(g =>
                g.Mods.Any(m =>
                    m.Name.ToLower().Contains(searchText) ||
                    m.Author.ToLower().Contains(searchText)
                ) || g.Name.ToLower().Contains(searchText)
            ).Select(g => new GroupWithMods
            {
                Name = g.Name,
                Rank = g.Rank,
                Mods = new ObservableCollection<ModInfo>(
                    g.Mods.Where(m =>
                        m.Name.ToLower().Contains(searchText) ||
                        m.Author.ToLower().Contains(searchText) ||
                        g.Name.ToLower().Contains(searchText)
                    )
                )
            }).ToList();

            CategoryTreeView.ItemsSource = filteredGroups;
        }
    }

    private async System.Threading.Tasks.Task InitPlugins()
    {
        try
        {
            StatusText.Text = "Loading mods...";

            using var client = new HttpClient();
            var rawGroups = await client.GetStringAsync(GROUPS_URL);
            var rawMods = await client.GetStringAsync(MODS_URL);

            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };

            var groups = JsonSerializer.Deserialize<List<GroupInfo>>(rawGroups, options) ?? new();
            var mods = JsonSerializer.Deserialize<RawMod[]>(rawMods, options) ?? Array.Empty<RawMod>();

            groupInfos = groups;
            allMods.Clear();

            foreach (var rawMod in mods)
            {
                var modInfo = new ModInfo
                {
                    Name = rawMod.Name,
                    Author = rawMod.Author,
                    Version = rawMod.Version,
                    GitPath = rawMod.GitPath,
                    GroupInfo = groups.FirstOrDefault(g => g.Name == rawMod.Group),
                    DownloadURL = rawMod.DownloadURL
                };
                allMods.Add(modInfo);
            }

            var grouped = allMods
                .GroupBy(m => m.GroupInfo?.Name ?? "Other")
                .OrderBy(g => groups.FirstOrDefault(gi => gi.Name == g.Key)?.Rank ?? int.MaxValue)
                .Select(g => new GroupWithMods
                {
                    Name = g.Key,
                    Rank = groups.FirstOrDefault(gi => gi.Name == g.Key)?.Rank ?? int.MaxValue,
                    Mods = new ObservableCollection<ModInfo>(g.OrderBy(m => m.Name))
                });

            groupedMods.Clear();
            foreach (var group in grouped)
                groupedMods.Add(group);

            CategoryTreeView.ItemsSource = groupedMods;
            ModsList.ItemsSource = allMods;

            CategoryFilter.ItemsSource = new[] { "All Categories" }
                .Concat(groups.OrderBy(g => g.Rank).Select(g => g.Name));

            ModCount.Text = $"{allMods.Count} mods";
            StatusText.Text = "Mods loaded successfully";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading mods: {ex.Message}";
        }
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose Gorilla Tag.exe"
        };
        var file = await StorageProvider.OpenFilePickerAsync(options);

        if (file == null || file.Count == 0 || file[0] == null) return;

        if (file[0].Name == "Gorilla Tag.exe")
        {
            if (gameInstallation != null)
                gameInstallation.Path = file[0].Path.ToString().Replace("/Gorilla Tag.exe", "");
            else
                gameInstallation = new GameInstallation(file[0].Path.ToString().Replace("/Gorilla Tag.exe", ""));

            StatusText.Text = $"Game path manually set to: {file[0].Path}.";
        }
        else
        {
            StatusText.Text = "Game not found in selected folder.";
        }
    }

    public static string GetOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";

        return "Unknown";
    }

    private void DetectGamePath()
    {
        string os = GetOS();
        string? steamBase = null;

        if (os == "Windows")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key != null)
                steamBase = key.GetValue("SteamPath") as string ?? key.GetValue("SteamInstallPath") as string;

            steamBase ??= @"C:\Program Files (x86)\Steam";
        }
        else if (os == "Linux")
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidates =
            {
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".local", "share", "Steam"),
            };

            steamBase = candidates.FirstOrDefault(Directory.Exists);
        }
        else if (os == "macOS")
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string candidate = Path.Combine(home, "Library", "Application Support", "Steam");
            if (Directory.Exists(candidate))
                steamBase = candidate;
        }

        if (!string.IsNullOrEmpty(steamBase))
        {
            string gorillaTagPath = Path.Combine(steamBase, "steamapps", "common", "Gorilla Tag");
            if (Directory.Exists(gorillaTagPath))
            {
                gameInstallation = new GameInstallation(gorillaTagPath);
                StatusText.Text = $"Game found at: {gameInstallation.Path}";
                GamePath.Text = $"Game Path: {gameInstallation.Path}";
                return;
            }
        }

        StatusText.Text = "Game not found. Please set manually.";
    }
}

public class GroupWithMods : GroupInfo
{
    public ObservableCollection<ModInfo> Mods { get; set; } = new();
}
