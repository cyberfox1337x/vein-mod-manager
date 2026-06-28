using VEIN_Item_And_Container_Modifier;

if (args.Length != 1)
{
    throw new InvalidOperationException("Usage: smoke-tests <ItemAndContainerModifier template folder>");
}

var template = Path.GetFullPath(args[0]);
if (!LuaModService.IsValidModFolder(template))
{
    throw new DirectoryNotFoundException("Template folder is not a valid ItemAndContainerModifier mod: " + template);
}

var testRoot = Path.Combine(Path.GetTempPath(), "vein-mod-manager-smoke-" + Guid.NewGuid().ToString("N"));
var modFolder = Path.Combine(testRoot, "ItemAndContainerModifier");

try
{
    CopyDirectory(template, modFolder);
    SeedExistingUiConfig(modFolder);

    var loaded = LuaModService.LoadUiConfigState(modFolder);
    AssertEqual(7, loaded.CountEdits(LuaModService.LoadModData(modFolder)), "loaded generated edit count");
    AssertNumber(999999m, loaded.CategoryDefaults["backpacks"]["ExtraWeightCapacity"], "backpack category default");
    AssertNumber(999999m, loaded.CategoryDefaults["containers"]["MaxWeight"], "container category default");
    AssertNumber(999999m, loaded.ItemOverrides["backpacks"]["BP_BackpackSchool_C"]["ExtraWeightCapacity"], "backpack item override capacity");
    AssertNumber(1m, loaded.ItemOverrides["backpacks"]["BP_BackpackSchool_C"]["RunSpeedMultiplier"], "backpack item override speed");
    AssertNumber(999999m, loaded.ContainerWeightOverrides["containers"]["BP_Fridge_Residential_C"], "fridge override");
    AssertNumber(999999m, loaded.ContainerWeightOverrides["vehicles"]["BP_BoxTruck_C"], "box truck override");

    loaded.ContainerWeightOverrides["vehicles"]["BP_Ambulance_C"] = new LuaValue(12345m);
    var backupPath = LuaModService.CreateBackup(modFolder);
    AssertFileExists(Path.Combine(backupPath, "config.lua"), "backup config.lua");
    AssertFileExists(Path.Combine(backupPath, "ui_config.lua"), "backup ui_config.lua");
    AssertFileExists(Path.Combine(backupPath, "categories", "vehicles.lua"), "backup vehicles.lua");

    LuaModService.ApplyConfig(modFolder, loaded);

    var reloaded = LuaModService.LoadUiConfigState(modFolder);
    AssertEqual(8, reloaded.CountEdits(LuaModService.LoadModData(modFolder)), "reloaded generated edit count");
    AssertNumber(999999m, reloaded.CategoryDefaults["backpacks"]["ExtraWeightCapacity"], "preserved backpack category default");
    AssertNumber(999999m, reloaded.ItemOverrides["backpacks"]["BP_BackpackSchool_C"]["ExtraWeightCapacity"], "preserved backpack item override capacity");
    AssertNumber(999999m, reloaded.ContainerWeightOverrides["containers"]["BP_Fridge_Residential_C"], "preserved fridge override");
    AssertNumber(12345m, reloaded.ContainerWeightOverrides["vehicles"]["BP_Ambulance_C"], "new ambulance override");

    var configText = File.ReadAllText(Path.Combine(modFolder, "Scripts", "config.lua"));
    if (!configText.Contains("UI_CONFIG_SUPPORT_BEGIN", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("config.lua was not patched with UI config support.");
    }

    var emptyStateFolder = Path.Combine(testRoot, "EmptyStateMod");
    CopyDirectory(template, emptyStateFolder);
    File.Delete(Path.Combine(emptyStateFolder, "Scripts", "ui_config.lua"));
    AssertEqual(0, LuaModService.LoadUiConfigState(emptyStateFolder).CountEdits(null), "missing ui_config edit count");

    var installTargetFolder = Path.Combine(testRoot, "InstallTargetMod");
    CopyDirectory(template, installTargetFolder);
    var sourceConfigPath = Path.Combine(testRoot, "import-source", "ui_config.lua");
    SeedImportSourceConfig(sourceConfigPath);
    var install = LuaModService.InstallUiConfig(installTargetFolder, sourceConfigPath);
    AssertFileExists(Path.Combine(install.BackupPath, "config.lua"), "install backup config.lua");
    AssertFileExists(install.InstalledPath, "installed ui_config.lua");

    var installed = LuaModService.LoadUiConfigState(installTargetFolder);
    AssertNumber(55555m, installed.CategoryDefaults["vehicles"]["MaxWeight"], "installed vehicle category max weight");
    AssertNumber(77777m, installed.ContainerWeightOverrides["vehicles"]["BP_Ambulance_C"], "installed ambulance override");

    Console.WriteLine("VEIN Mod Manager smoke tests passed.");
}
finally
{
    if (Directory.Exists(testRoot))
    {
        Directory.Delete(testRoot, recursive: true);
    }
}

static void SeedExistingUiConfig(string modFolder)
{
    var uiConfigPath = Path.Combine(modFolder, "Scripts", "ui_config.lua");
    File.WriteAllText(uiConfigPath, """
-- ui_config.lua
-- Existing generated user config.
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

static void SeedImportSourceConfig(string uiConfigPath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(uiConfigPath)!);
    File.WriteAllText(uiConfigPath, """
-- ui_config.lua
-- Import/install smoke test source.
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

static void CopyDirectory(string source, string destination)
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

static void AssertFileExists(string path, string label)
{
    if (!File.Exists(path))
    {
        throw new InvalidOperationException("Missing " + label + ": " + path);
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
    where T : IEquatable<T>
{
    if (!actual.Equals(expected))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}

static void AssertNumber(decimal expected, LuaValue actual, string label)
{
    if (actual.Value is not decimal number || number != expected)
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual.ToLua()}");
    }
}
