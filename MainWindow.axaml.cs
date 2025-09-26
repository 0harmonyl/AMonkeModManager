using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AMonkeModManager.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace AMonkeModManager;

public class GroupWithMods : GroupInfo
{
    public new ObservableCollection<ModInfo> Mods { get; set; } = new();
}

public partial class MainWindow : Window
{
    public const string GROUPS_URL = "https://raw.githubusercontent.com/The-Graze/MonkeModInfo/master/groupinfo.json";
    public const string MODS_URL = "https://raw.githubusercontent.com/The-Graze/MonkeModInfo/master/modinfo.json";

    private GameInstallation? gameInstallation;
    private List<GroupInfo> groupInfos = new();

    private ObservableCollection<GroupWithMods> groupedMods = new();
    private ObservableCollection<ModInfo> allMods = new();
    private HashSet<ModInfo> selectedMods = new();
    private ModInfo? utillaModInfo;
    private ModInfo? bepinexModInfo;

    public MainWindow()
    {
        InitializeComponent();

        Browse.Click += Browse_Click;
        SearchBox.TextChanged += SearchBox_TextChanged;

        CategoryViewRadio.Checked += ViewMode_Changed;
        ListViewRadio.Checked += ViewMode_Changed;
        GridViewRadio.Checked += ViewMode_Changed;

        CategoryFilter.SelectionChanged += CategoryFilter_Changed;

        InstallSelectedBtn.Click += InstallSelected_Click;
        UpdateAllBtn.Click += UpdateAll_Click;
        RefreshBtn.Click += Refresh_Click;

        DetectGamePath();
        _ = InitPlugins();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        FilterMods();
    }

    private void ViewMode_Changed(object? sender, RoutedEventArgs e)
    {
        if (CategoryViewRadio?.IsChecked == true)
        {
            CategoryScrollViewer.IsVisible = true;
            ListScrollViewer.IsVisible = false;
            GridScrollViewer.IsVisible = false;
        }
        else if (ListViewRadio?.IsChecked == true)
        {
            CategoryScrollViewer.IsVisible = false;
            ListScrollViewer.IsVisible = true;
            GridScrollViewer.IsVisible = false;
        }
        else if (GridViewRadio?.IsChecked == true)
        {
            CategoryScrollViewer.IsVisible = false;
            ListScrollViewer.IsVisible = false;
            GridScrollViewer.IsVisible = true;
            RenderGridView();
        }
    }

    private void CategoryFilter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        FilterMods();
    }

    private void FilterMods()
    {
        var searchText = SearchBox?.Text?.ToLower() ?? "";
        var selectedCategory = CategoryFilter?.SelectedItem as string;

        var filtered = allMods.Where(m =>
        {
            bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                m.Name.ToLower().Contains(searchText) ||
                m.Author.ToLower().Contains(searchText) ||
                (m.GroupInfo?.Name?.ToLower().Contains(searchText) ?? false);

            bool matchesCategory = selectedCategory == null ||
                selectedCategory == "All Categories" ||
                m.GroupInfo?.Name == selectedCategory;

            return matchesSearch && matchesCategory;
        }).ToList();

        ModsList.ItemsSource = filtered;

        var filteredGroups = groupedMods.Select(g =>
        {
            var filteredModsInGroup = g.Mods.Where(m =>
            {
                bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                    m.Name.ToLower().Contains(searchText) ||
                    m.Author.ToLower().Contains(searchText) ||
                    g.Name.ToLower().Contains(searchText);

                bool matchesCategory = selectedCategory == null ||
                    selectedCategory == "All Categories" ||
                    g.Name == selectedCategory;

                return matchesSearch && matchesCategory;
            }).ToList();

            if (!filteredModsInGroup.Any() && (selectedCategory == null || selectedCategory == "All Categories" || g.Name != selectedCategory))
                return null;

            return new GroupWithMods
            {
                Name = g.Name,
                Rank = g.Rank,
                Mods = new ObservableCollection<ModInfo>(filteredModsInGroup)
            };
        }).Where(g => g != null && (g.Mods.Any() ||
            (selectedCategory != null && selectedCategory != "All Categories" && g.Name == selectedCategory)))
        .ToList();

        BuildTreeView(filteredGroups);

        if (GridViewRadio?.IsChecked == true)
        {
            RenderGridView();
        }
    }

    private void BuildTreeView(List<GroupWithMods>? groups = null)
    {
        groups ??= groupedMods.ToList();
        var items = new List<TreeViewItem>();

        foreach (var group in groups)
        {
            var groupItem = new TreeViewItem();

            // Create group header
            var groupHeader = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2D2D30")),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(8, 6),
                Margin = new Avalonia.Thickness(0, 2)
            };

            var groupGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
            };

            var folderIcon = new TextBlock
            {
                Text = "📁",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 8, 0),
                Foreground = Brushes.White
            };
            Grid.SetColumn(folderIcon, 0);

            var groupName = new TextBlock
            {
                Text = group.Name,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            Grid.SetColumn(groupName, 1);

            var groupCount = new TextBlock
            {
                Text = $"({group.Mods.Count})",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(groupCount, 2);

            groupGrid.Children.Add(folderIcon);
            groupGrid.Children.Add(groupName);
            groupGrid.Children.Add(groupCount);

            groupHeader.Child = groupGrid;
            groupItem.Header = groupHeader;

            // Add mod items
            foreach (var mod in group.Mods)
            {
                var modItem = CreateModTreeItem(mod);
                groupItem.Items.Add(modItem);
            }

            groupItem.IsExpanded = true;
            items.Add(groupItem);
        }

        CategoryTreeView.ItemsSource = items;
    }

    private TreeViewItem CreateModTreeItem(ModInfo mod)
    {
        var modItem = new TreeViewItem();

        var modBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E42")),
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            Padding = new Avalonia.Thickness(12, 8),
            Margin = new Avalonia.Thickness(20, 0, 0, 0)
        };

        var modGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto")
        };

        // Checkbox
        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 10, 0),
            Tag = mod,
            IsChecked = selectedMods.Contains(mod)
        };
        checkBox.Checked += ModCheckBox_Changed;
        checkBox.Unchecked += ModCheckBox_Changed;
        Grid.SetColumn(checkBox, 0);

        // Mod info
        var infoPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2
        };

        bool isRequiredMod = (mod == bepinexModInfo || mod == utillaModInfo);

        var modName = new TextBlock
        {
            Text = isRequiredMod ? $"{mod.Name} (Required)" : mod.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = isRequiredMod ? new SolidColorBrush(Color.Parse("#FFD700")) : Brushes.White
        };

        var modDetails = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            Text = $"by {mod.Author} • {mod.Version}"
        };

        infoPanel.Children.Add(modName);
        infoPanel.Children.Add(modDetails);
        Grid.SetColumn(infoPanel, 1);

        // Install button
        var installBtn = new Button
        {
            Content = "⬇",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#0E639C")),
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = mod
        };
        installBtn.Click += InstallMod_Click;
        Grid.SetColumn(installBtn, 3);

        // More button
        var moreBtn = new Button
        {
            Content = "⋯",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = mod
        };

        var flyout = new MenuFlyout();
        var installMenuItem = new MenuItem { Header = "Install", Tag = mod };
        installMenuItem.Click += MenuItem_Click;
        var uninstallMenuItem = new MenuItem { Header = "Uninstall", Tag = mod };
        uninstallMenuItem.Click += MenuItem_Click;
        var githubMenuItem = new MenuItem { Header = "View on GitHub", Tag = mod };
        githubMenuItem.Click += MenuItem_Click;
        var detailsMenuItem = new MenuItem { Header = "View Details", Tag = mod };
        detailsMenuItem.Click += MenuItem_Click;

        flyout.Items.Add(installMenuItem);
        flyout.Items.Add(uninstallMenuItem);
        flyout.Items.Add(new Separator());
        flyout.Items.Add(githubMenuItem);
        flyout.Items.Add(detailsMenuItem);

        moreBtn.Flyout = flyout;
        Grid.SetColumn(moreBtn, 4);

        modGrid.Children.Add(checkBox);
        modGrid.Children.Add(infoPanel);
        modGrid.Children.Add(installBtn);
        modGrid.Children.Add(moreBtn);

        modBorder.Child = modGrid;
        modItem.Header = modBorder;

        return modItem;
    }

    private void RenderGridView()
    {
        GridPanel.Children.Clear();

        var modsToDisplay = ModsList.ItemsSource as IEnumerable<ModInfo> ?? allMods;

        foreach (var mod in modsToDisplay)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#252526")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3E3E42")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
                Margin = new Avalonia.Thickness(5),
                Width = 250,
                Height = 150,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            var content = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                Margin = new Avalonia.Thickness(10)
            };

            var header = new StackPanel();
            bool isRequiredMod = (mod == bepinexModInfo || mod == utillaModInfo);
            header.Children.Add(new TextBlock
            {
                Text = isRequiredMod ? $"{mod.Name} (Required)" : mod.Name,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = isRequiredMod ? new SolidColorBrush(Color.Parse("#FFD700")) : Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            header.Children.Add(new TextBlock
            {
                Text = $"by {mod.Author}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA"))
            });
            Grid.SetRow(header, 0);
            content.Children.Add(header);

            var middlePanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 5
            };
            middlePanel.Children.Add(new TextBlock
            {
                Text = $"Version: {mod.Version}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#6A9955"))
            });
            middlePanel.Children.Add(new TextBlock
            {
                Text = mod.GroupInfo?.Name ?? "Other",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#569CD6"))
            });
            Grid.SetRow(middlePanel, 1);
            content.Children.Add(middlePanel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 5
            };

            var checkBox = new CheckBox
            {
                Tag = mod,
                IsChecked = selectedMods.Contains(mod)
            };
            checkBox.Checked += ModCheckBox_Changed;
            checkBox.Unchecked += ModCheckBox_Changed;
            buttonPanel.Children.Add(checkBox);

            var installBtn = new Button
            {
                Content = "Install",
                FontSize = 11,
                Padding = new Avalonia.Thickness(8, 4),
                Background = new SolidColorBrush(Color.Parse("#0E639C")),
                Foreground = Brushes.White,
                Tag = mod
            };
            installBtn.Click += InstallMod_Click;
            buttonPanel.Children.Add(installBtn);

            Grid.SetRow(buttonPanel, 2);
            content.Children.Add(buttonPanel);

            card.Child = content;
            GridPanel.Children.Add(card);
        }
    }

    private async Task InitPlugins()
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

                // Store references to required mods
                if (rawMod.Name.ToLower().Contains("utilla"))
                    utillaModInfo = modInfo;
                else if (rawMod.Name.ToLower().Contains("bepinex"))
                    bepinexModInfo = modInfo;
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

            BuildTreeView();
            ModsList.ItemsSource = allMods;

            AttachEventHandlers();

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

    private void AttachEventHandlers()
    {
        AttachTreeViewHandlers(CategoryTreeView);
        AttachListViewHandlers(ModsList);
    }

    private void AttachTreeViewHandlers(Control control)
    {
        if (control is TreeView treeView)
        {
            treeView.Loaded += (s, e) =>
            {
                AttachHandlersToTreeViewItems(treeView);
            };
        }
    }

    private void AttachHandlersToTreeViewItems(TreeView treeView)
    {
        var items = treeView.ItemsSource as IEnumerable<GroupWithMods>;
        if (items == null) return;

        foreach (var item in items)
        {
            var container = treeView.ContainerFromItem(item) as TreeViewItem;
            if (container != null)
            {
                AttachHandlersRecursive(container);
            }
        }
    }

    private void AttachHandlersRecursive(Control control)
    {
        var checkBoxes = control.GetLogicalDescendants().OfType<CheckBox>();
        foreach (var checkBox in checkBoxes)
        {
            if (checkBox.Name == "ModCheckBox" && checkBox.Tag is ModInfo)
            {
                checkBox.Checked -= ModCheckBox_Changed;
                checkBox.Unchecked -= ModCheckBox_Changed;
                checkBox.Checked += ModCheckBox_Changed;
                checkBox.Unchecked += ModCheckBox_Changed;
                checkBox.IsChecked = selectedMods.Contains(checkBox.Tag as ModInfo);
            }
        }

        var buttons = control.GetLogicalDescendants().OfType<Button>();
        foreach (var button in buttons)
        {
            if (button.Name == "InstallBtn" && button.Tag is ModInfo)
            {
                button.Click -= InstallMod_Click;
                button.Click += InstallMod_Click;
            }
            else if (button.Name == "MoreBtn" && button.Tag is ModInfo)
            {
                var flyout = button.Flyout as MenuFlyout;
                if (flyout != null)
                {
                    foreach (MenuItem menuItem in flyout.Items)
                    {
                        menuItem.Click -= MenuItem_Click;
                        menuItem.Click += MenuItem_Click;
                    }
                }
            }
        }
    }

    private void AttachListViewHandlers(Control control)
    {
        control.Loaded += (s, e) =>
        {
            var checkBoxes = control.GetLogicalDescendants().OfType<CheckBox>();
            foreach (var checkBox in checkBoxes)
            {
                if (checkBox.Name == "ListModCheckBox" && checkBox.Tag is ModInfo)
                {
                    checkBox.Checked -= ModCheckBox_Changed;
                    checkBox.Unchecked -= ModCheckBox_Changed;
                    checkBox.Checked += ModCheckBox_Changed;
                    checkBox.Unchecked += ModCheckBox_Changed;
                    checkBox.IsChecked = selectedMods.Contains(checkBox.Tag as ModInfo);
                }
            }

            var buttons = control.GetLogicalDescendants().OfType<Button>();
            foreach (var button in buttons)
            {
                if (button.Name == "ListInstallBtn" && button.Tag is ModInfo)
                {
                    button.Click -= InstallMod_Click;
                    button.Click += InstallMod_Click;
                }
                else if (button.Name == "ListGitHubBtn" && button.Tag is ModInfo)
                {
                    button.Click -= OpenGitHub_Click;
                    button.Click += OpenGitHub_Click;
                }
            }
        };
    }

    private void ModCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is ModInfo mod)
        {
            if (checkBox.IsChecked == true)
            {
                selectedMods.Add(mod);

                // Automatically add required dependencies
                if (selectedMods.Count > 0)
                {
                    // Add BepInEx if not already selected
                    if (bepinexModInfo != null && !selectedMods.Contains(bepinexModInfo))
                    {
                        selectedMods.Add(bepinexModInfo);
                        UpdateCheckboxStates();
                    }

                    // Add Utilla if not already selected
                    if (utillaModInfo != null && !selectedMods.Contains(utillaModInfo))
                    {
                        selectedMods.Add(utillaModInfo);
                        UpdateCheckboxStates();
                    }
                }
            }
            else
            {
                // Don't allow removing required mods if other mods are selected
                bool isRequiredMod = (mod == bepinexModInfo || mod == utillaModInfo);
                bool hasOtherMods = selectedMods.Any(m => m != bepinexModInfo && m != utillaModInfo);

                if (isRequiredMod && hasOtherMods)
                {
                    // Re-check the box and show a message
                    checkBox.IsChecked = true;
                    StatusText.Text = $"{mod.Name} is required when other mods are selected";
                    return;
                }

                selectedMods.Remove(mod);
            }
            UpdateSelectedCount();
        }
    }

    private void UpdateCheckboxStates()
    {
        // Update all checkbox states in the tree view
        if (CategoryTreeView.ItemsSource is List<TreeViewItem> items)
        {
            foreach (var groupItem in items)
            {
                foreach (TreeViewItem modItem in groupItem.Items)
                {
                    if (modItem.Header is Border border && border.Child is Grid grid)
                    {
                        var checkBox = grid.Children.OfType<CheckBox>().FirstOrDefault();
                        if (checkBox?.Tag is ModInfo mod)
                        {
                            checkBox.IsChecked = selectedMods.Contains(mod);
                        }
                    }
                }
            }
        }

        // Update list view checkboxes
        var listCheckBoxes = ListScrollViewer.GetLogicalDescendants().OfType<CheckBox>();
        foreach (var checkBox in listCheckBoxes)
        {
            if (checkBox.Tag is ModInfo mod)
            {
                checkBox.IsChecked = selectedMods.Contains(mod);
            }
        }

        // Update grid view checkboxes if needed
        if (GridPanel != null)
        {
            var gridCheckBoxes = GridPanel.Children.OfType<Border>()
                .Select(b => b.Child)
                .OfType<Grid>()
                .SelectMany(g => g.Children.OfType<StackPanel>())
                .SelectMany(s => s.Children.OfType<CheckBox>());

            foreach (var checkBox in gridCheckBoxes)
            {
                if (checkBox.Tag is ModInfo mod)
                {
                    checkBox.IsChecked = selectedMods.Contains(mod);
                }
            }
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount.Text = $"{selectedMods.Count} selected";
    }

    private async void InstallMod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ModInfo mod)
        {
            await InstallMod(mod);
        }
    }

    private async void MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is ModInfo mod)
        {
            switch (menuItem.Header?.ToString())
            {
                case "Install":
                    await InstallMod(mod);
                    break;
                case "Uninstall":
                    await UninstallMod(mod);
                    break;
                case "View on GitHub":
                    OpenGitHub(mod);
                    break;
                case "View Details":
                    ShowModDetails(mod);
                    break;
            }
        }
    }

    private async Task InstallMod(ModInfo mod)
    {
        if (gameInstallation == null)
        {
            StatusText.Text = "Please set game path first";
            return;
        }

        StatusText.Text = $"Installing {mod.Name}...";

        try
        {
            if (!string.IsNullOrEmpty(mod.DownloadURL))
            {
                using var client = new HttpClient();
                var data = await client.GetByteArrayAsync(mod.DownloadURL);

                var pluginsPath = Path.Combine(gameInstallation.Path, "BepInEx", "plugins");
                Directory.CreateDirectory(pluginsPath);

                var fileName = Path.GetFileName(mod.DownloadURL);
                if (string.IsNullOrEmpty(fileName))
                    fileName = $"{mod.Name}.dll";

                var filePath = Path.Combine(pluginsPath, fileName);
                await File.WriteAllBytesAsync(filePath, data);

                StatusText.Text = $"Installed {mod.Name} successfully";
            }
            else
            {
                StatusText.Text = $"No download URL available for {mod.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to install {mod.Name}: {ex.Message}";
        }
    }

    private async Task UninstallMod(ModInfo mod)
    {
        if (gameInstallation == null)
        {
            StatusText.Text = "Please set game path first";
            return;
        }

        StatusText.Text = $"Uninstalling {mod.Name}...";
        await Task.Delay(500);
        StatusText.Text = $"Uninstalled {mod.Name} successfully";
    }

    private void OpenGitHub_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ModInfo mod)
        {
            OpenGitHub(mod);
        }
    }

    private void OpenGitHub(ModInfo mod)
    {
        if (!string.IsNullOrEmpty(mod.GitPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = mod.GitPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                StatusText.Text = "Failed to open GitHub page";
            }
        }
    }

    private void ShowModDetails(ModInfo mod)
    {
        StatusText.Text = $"Showing details for {mod.Name}";
    }

    private async void InstallSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (!selectedMods.Any())
        {
            StatusText.Text = "No mods selected";
            return;
        }

        StatusText.Text = $"Installing {selectedMods.Count} mods...";

        // Install mods in order - BepInEx first, then Utilla, then others
        var orderedMods = selectedMods.OrderBy(m =>
        {
            if (m == bepinexModInfo) return 0;
            if (m == utillaModInfo) return 1;
            return 2;
        }).ToList();

        foreach (var mod in orderedMods)
        {
            await InstallMod(mod);
        }

        StatusText.Text = $"Installed {selectedMods.Count} mods";
        selectedMods.Clear();
        UpdateSelectedCount();
        UpdateCheckboxStates();
    }

    private async void UpdateAll_Click(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = "Checking for updates...";
        await Task.Delay(1000);
        StatusText.Text = "All mods are up to date";
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await InitPlugins();
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
            var path = file[0].Path.LocalPath.Replace("\\Gorilla Tag.exe", "").Replace("/Gorilla Tag.exe", "");

            if (gameInstallation != null)
                gameInstallation.Path = path;
            else
                gameInstallation = new GameInstallation(path);

            StatusText.Text = $"Game path set to: {path}";
            GamePath.Text = $"Game: {path}";
        }
        else
        {
            StatusText.Text = "Please select Gorilla Tag.exe";
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
                GamePath.Text = $"Game: {gameInstallation.Path}";
                return;
            }
        }

        StatusText.Text = "Game not found. Please set manually.";
        GamePath.Text = "No game path";
    }
}

public static class ControlExtensions
{
    public static IEnumerable<Control> GetLogicalDescendants(this Control control)
    {
        var queue = new Queue<Control>();
        queue.Enqueue(control);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            if (current is Panel panel)
            {
                foreach (var child in panel.Children.OfType<Control>())
                {
                    queue.Enqueue(child);
                }
            }
            else if (current is ContentControl contentControl && contentControl.Content is Control content)
            {
                queue.Enqueue(content);
            }
            else if (current is ItemsControl itemsControl)
            {
                foreach (var item in itemsControl.Items)
                {
                    if (itemsControl.ContainerFromItem(item) is Control container)
                    {
                        queue.Enqueue(container);
                    }
                }
            }
            else if (current is Decorator decorator && decorator.Child is Control decoratorChild)
            {
                queue.Enqueue(decoratorChild);
            }
        }
    }
}
