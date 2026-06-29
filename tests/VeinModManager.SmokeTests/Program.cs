using System.Text.RegularExpressions;
using VEIN_Item_And_Container_Modifier;
using Xunit;

public sealed class SmokeTests
{
    private static readonly Regex KeyEntryPattern = new(@"\[""(?<key>[^""]+)""\]\s*=\s*\{(?<body>.*?)(?=^\s*\},?)", RegexOptions.Multiline | RegexOptions.Singleline);
    private static readonly Regex ClassBlockPattern = new(@"classes\s*=\s*\{(?<body>.*?)(?=^\s*\},?)", RegexOptions.Multiline | RegexOptions.Singleline);
    private static readonly Regex QuotedStringPattern = new(@"""(?<value>[^""]+)""", RegexOptions.Multiline);
    private static readonly Regex CdoPathPattern = new(@"_CDOPath\s*=\s*""(?<path>[^""]+)""", RegexOptions.Multiline);

    [Fact]
    public void SmokeWorkflow_LoadsAppliesBacksUpAndInstallsUiConfig()
    {
        var template = GetTemplateFolder();
        var testRoot = CreateTempRoot();
        var modFolder = Path.Combine(testRoot, "ItemAndContainerModifier");

        try
        {
            CopyDirectory(template, modFolder);
            SeedExistingUiConfig(modFolder);

            var loaded = LuaModService.LoadUiConfigState(modFolder);
            Assert.Equal(7, loaded.CountEdits(LuaModService.LoadModData(modFolder)));
            AssertNumber(999999m, loaded.CategoryDefaults["backpacks"]["ExtraWeightCapacity"]);
            AssertNumber(999999m, loaded.CategoryDefaults["containers"]["MaxWeight"]);
            AssertNumber(999999m, loaded.ItemOverrides["backpacks"]["BP_BackpackSchool_C"]["ExtraWeightCapacity"]);
            AssertNumber(1m, loaded.ItemOverrides["backpacks"]["BP_BackpackSchool_C"]["RunSpeedMultiplier"]);
            AssertNumber(999999m, loaded.ContainerWeightOverrides["containers"]["BP_Fridge_Residential_C"]);
            AssertNumber(999999m, loaded.ContainerWeightOverrides["vehicles"]["BP_BoxTruck_C"]);

            loaded.ContainerWeightOverrides["vehicles"]["BP_Ambulance_C"] = new LuaValue(12345m);
            var backupPath = LuaModService.CreateBackup(modFolder);
            AssertFileExists(Path.Combine(backupPath, "config.lua"));
            AssertFileExists(Path.Combine(backupPath, "ui_config.lua"));
            AssertFileExists(Path.Combine(backupPath, "categories", "vehicles.lua"));

            LuaModService.ApplyConfig(modFolder, loaded);

            var reloaded = LuaModService.LoadUiConfigState(modFolder);
            Assert.Equal(8, reloaded.CountEdits(LuaModService.LoadModData(modFolder)));
            AssertNumber(999999m, reloaded.CategoryDefaults["backpacks"]["ExtraWeightCapacity"]);
            AssertNumber(999999m, reloaded.ItemOverrides["backpacks"]["BP_BackpackSchool_C"]["ExtraWeightCapacity"]);
            AssertNumber(999999m, reloaded.ContainerWeightOverrides["containers"]["BP_Fridge_Residential_C"]);
            AssertNumber(12345m, reloaded.ContainerWeightOverrides["vehicles"]["BP_Ambulance_C"]);

            var configText = File.ReadAllText(Path.Combine(modFolder, "Scripts", "config.lua"));
            Assert.Contains("UI_CONFIG_SUPPORT_BEGIN", configText, StringComparison.Ordinal);

            var emptyStateFolder = Path.Combine(testRoot, "EmptyStateMod");
            CopyDirectory(template, emptyStateFolder);
            File.Delete(Path.Combine(emptyStateFolder, "Scripts", "ui_config.lua"));
            Assert.Equal(0, LuaModService.LoadUiConfigState(emptyStateFolder).CountEdits(null));

            var installTargetFolder = Path.Combine(testRoot, "InstallTargetMod");
            CopyDirectory(template, installTargetFolder);
            var sourceConfigPath = Path.Combine(testRoot, "import-source", "ui_config.lua");
            SeedImportSourceConfig(sourceConfigPath);
            var install = LuaModService.InstallUiConfig(installTargetFolder, sourceConfigPath);
            AssertFileExists(Path.Combine(install.BackupPath, "config.lua"));
            AssertFileExists(install.InstalledPath);

            var installed = LuaModService.LoadUiConfigState(installTargetFolder);
            AssertNumber(55555m, installed.CategoryDefaults["vehicles"]["MaxWeight"]);
            AssertNumber(77777m, installed.ContainerWeightOverrides["vehicles"]["BP_Ambulance_C"]);
        }
        finally
        {
            DeleteDirectoryIfExists(testRoot);
        }
    }

    [Fact]
    public void CreateBackup_ConsecutiveCallsUseUniqueFolders()
    {
        var template = GetTemplateFolder();
        var testRoot = CreateTempRoot();
        var modFolder = Path.Combine(testRoot, "ItemAndContainerModifier");

        try
        {
            CopyDirectory(template, modFolder);

            var firstBackup = LuaModService.CreateBackup(modFolder);
            var secondBackup = LuaModService.CreateBackup(modFolder);

            Assert.NotEqual(firstBackup, secondBackup);
            Assert.Equal(Path.Combine(modFolder, "Backups"), Directory.GetParent(firstBackup)!.FullName);
            Assert.Equal(Path.Combine(modFolder, "Backups"), Directory.GetParent(secondBackup)!.FullName);
            Assert.True(Directory.Exists(firstBackup), "Missing first backup folder: " + firstBackup);
            Assert.True(Directory.Exists(secondBackup), "Missing second backup folder: " + secondBackup);
            AssertFileExists(Path.Combine(firstBackup, "config.lua"));
            AssertFileExists(Path.Combine(secondBackup, "config.lua"));
        }
        finally
        {
            DeleteDirectoryIfExists(testRoot);
        }
    }

    [Fact]
    public void CategoryTemplates_DoNotContainDuplicateKeysOrClasses()
    {
        var duplicates = ReadCategoryIdentifiers()
            .GroupBy(identifier => new { identifier.FileName, identifier.Kind, identifier.Value })
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.FileName + " " + group.Key.Kind + " " + group.Key.Value)
            .ToArray();

        Assert.True(duplicates.Length == 0, "Duplicate category keys/classes found within a template:" + Environment.NewLine + string.Join(Environment.NewLine, duplicates));
    }

    [Fact]
    public void CategoryTemplates_CdoPathTailsMatchKeys()
    {
        var mismatches = ReadCdoPathEntries()
            .Select(entry => new { Entry = entry, Tail = GetCdoPathTail(entry.CdoPath) })
            .Where(entry => !StringComparer.Ordinal.Equals(entry.Entry.Key, entry.Tail))
            .Select(entry => entry.Entry.FileName + ": " + entry.Entry.Key + " != " + entry.Tail)
            .ToArray();

        Assert.True(mismatches.Length == 0, "_CDOPath tails do not match category keys:" + Environment.NewLine + string.Join(Environment.NewLine, mismatches));
    }

    private static string GetTemplateFolder()
    {
        foreach (var root in CandidateRoots())
        {
            foreach (var relativePath in new[]
            {
                Path.Combine("src", "VeinModManager", "ModTemplate", "ItemAndContainerModifier"),
                Path.Combine("ModTemplate", "ItemAndContainerModifier")
            })
            {
                var candidate = Path.Combine(root, relativePath);
                if (LuaModService.IsValidModFolder(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate ItemAndContainerModifier template folder.");
    }

    private static string GetSourceCategoryFolder()
    {
        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "src", "VeinModManager", "ModTemplate", "ItemAndContainerModifier", "Scripts", "categories");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not locate source category templates.");
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (seen.Add(directory.FullName))
                {
                    yield return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
    }

    private static IReadOnlyList<CategoryIdentifier> ReadCategoryIdentifiers()
    {
        var identifiers = new List<CategoryIdentifier>();
        foreach (var path in Directory.EnumerateFiles(GetSourceCategoryFolder(), "*.lua", SearchOption.TopDirectoryOnly).OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            var text = StripLuaLineComments(File.ReadAllText(path));
            var fileName = Path.GetFileName(path);

            foreach (Match match in KeyEntryPattern.Matches(text))
            {
                identifiers.Add(new CategoryIdentifier(match.Groups["key"].Value, "key", fileName));
            }

            foreach (Match block in ClassBlockPattern.Matches(text))
            {
                foreach (Match match in QuotedStringPattern.Matches(block.Groups["body"].Value))
                {
                    identifiers.Add(new CategoryIdentifier(match.Groups["value"].Value, "class", fileName));
                }
            }
        }

        return identifiers;
    }

    private static IReadOnlyList<CdoPathEntry> ReadCdoPathEntries()
    {
        var entries = new List<CdoPathEntry>();
        foreach (var path in Directory.EnumerateFiles(GetSourceCategoryFolder(), "*.lua", SearchOption.TopDirectoryOnly).OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            var text = StripLuaLineComments(File.ReadAllText(path));
            var fileName = Path.GetFileName(path);

            foreach (Match match in KeyEntryPattern.Matches(text))
            {
                var cdoPath = CdoPathPattern.Match(match.Groups["body"].Value);
                if (cdoPath.Success)
                {
                    entries.Add(new CdoPathEntry(match.Groups["key"].Value, cdoPath.Groups["path"].Value, fileName));
                }
            }
        }

        return entries;
    }

    private static string StripLuaLineComments(string text)
    {
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var commentStart = lines[index].IndexOf("--", StringComparison.Ordinal);
            if (commentStart >= 0)
            {
                lines[index] = lines[index][..commentStart];
            }
        }

        return string.Join('\n', lines);
    }

    private static string GetCdoPathTail(string cdoPath)
    {
        const string marker = "Default__";
        var markerIndex = cdoPath.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            return cdoPath[(markerIndex + marker.Length)..];
        }

        var separatorIndex = cdoPath.LastIndexOfAny(new[] { '/', '\\' });
        var fileName = separatorIndex >= 0 ? cdoPath[(separatorIndex + 1)..] : cdoPath;
        var dotIndex = fileName.LastIndexOf('.');
        return dotIndex >= 0 ? fileName[(dotIndex + 1)..] : fileName;
    }

    private static void SeedExistingUiConfig(string modFolder)
    {
        var uiConfigPath = Path.Combine(modFolder, "Scripts", "ui_config.lua");
        File.WriteAllText(uiConfigPath, """
local UiConfig = {
    EnabledCategories = {
        vehicles = true,
        containers = true,
        backpacks = true,
    },

    CategoryDefaults = {
        backpacks = {
            ExtraWeightCapacity = 999999,
        },
        containers = 999999,
        vehicles = 999999,
    },

    ItemOverrides = {
        backpacks = {
            ["BP_BackpackSchool_C"] = {
                ExtraWeightCapacity = 999999,
                RunSpeedMultiplier = 1,
            },
        },
    },

    ContainerWeightOverrides = {
        containers = {
            ["BP_Fridge_Residential_C"] = 999999,
        },
        vehicles = {
            ["BP_BoxTruck_C"] = 999999,
        },
    },
}

return UiConfig
""");
    }

    private static void SeedImportSourceConfig(string uiConfigPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(uiConfigPath)!);
        File.WriteAllText(uiConfigPath, """
local UiConfig = {
    CategoryDefaults = {
        vehicles = 55555,
    },

    ContainerWeightOverrides = {
        vehicles = {
            ["BP_Ambulance_C"] = 77777,
        },
    },
}

return UiConfig
""");
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "vein-mod-manager-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void AssertFileExists(string path)
    {
        Assert.True(File.Exists(path), "Missing file: " + path);
    }

    private static void AssertNumber(decimal expected, LuaValue actual)
    {
        var number = Assert.IsType<decimal>(actual.Value);
        Assert.Equal(expected, number);
    }

    private sealed record CategoryIdentifier(string Value, string Kind, string FileName);

    private sealed record CdoPathEntry(string Key, string CdoPath, string FileName);
}
