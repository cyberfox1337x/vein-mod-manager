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
    AssertFileContains(Path.Combine(testRoot, "mods.txt"), "ItemAndContainerModifier : 1", "enabled mod entry");

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

    var helperPackage = ServerManagerService.GenerateLinuxHelperPackage();
    AssertFileExists(helperPackage.ZipPath, "generated Linux helper zip");
    AssertFileExists(Path.Combine(helperPackage.FolderPath, helperPackage.ScriptName), "generated Linux helper script");
    AssertFileContains(Path.Combine(helperPackage.FolderPath, helperPackage.ScriptName), "backup_config", "Linux helper backup behavior");
    AssertFileContains(Path.Combine(helperPackage.FolderPath, helperPackage.ScriptName), "write-config", "Linux helper write behavior");
    AssertFileContains(Path.Combine(helperPackage.FolderPath, helperPackage.ScriptName), "restart)", "Linux helper fixed restart command");

    var passwordLinuxProfile = new LinuxServerProfile(
        "127.0.0.1",
        22,
        "steam",
        "Password",
        "",
        "do-not-save-this",
        "/srv/vein",
        "/srv/vein/Vein/Saved/Config/LinuxServer/Game.ini");
    var linuxProfilePath = ServerManagerService.SaveLinuxProfile(passwordLinuxProfile);
    AssertFileExists(linuxProfilePath, "Linux server profile");
    AssertFileDoesNotContain(linuxProfilePath, "do-not-save-this", "Linux profile password omission");
    AssertThrowsContains(
        () => ServerManagerService.RunLinuxHelperCommand(passwordLinuxProfile, "status"),
        "SSH key authentication is required",
        "password auth helper command rejection");

    var serverRoot = Path.Combine(testRoot, "WindowsServer");
    var windowsProfile = new WindowsServerProfile(
        serverRoot,
        Path.Combine(testRoot, "steamcmd.exe"),
        "Smoke Server",
        "Generated by smoke test",
        "Server",
        "server-pass",
        "/Game/Vein/Maps/ChamplainValley?listen",
        7779,
        27015,
        16,
        true,
        27020,
        "rcon-pass",
        true,
        8080,
        "123456789");
    File.WriteAllText(windowsProfile.SteamCmdPath, "fake steamcmd");
    var windowsProfilePath = ServerManagerService.SaveWindowsProfile(windowsProfile);
    AssertFileDoesNotContain(windowsProfilePath, "server-pass", "Windows profile server password omission");
    AssertFileDoesNotContain(windowsProfilePath, "rcon-pass", "Windows profile RCON password omission");
    var updateStartInfo = ServerManagerService.CreateWindowsValidateOrUpdateStartInfo(windowsProfile);
    AssertEqual(windowsProfile.SteamCmdPath, updateStartInfo.FileName, "SteamCMD executable path");
    AssertSequenceContains(updateStartInfo.ArgumentList, new[] { "+app_update", "1857950", "validate" }, "SteamCMD validate arguments");
    AssertSequenceContains(updateStartInfo.ArgumentList, new[] { "+force_install_dir", serverRoot }, "SteamCMD install directory arguments");

    var windowsConfigPath = ServerManagerService.WriteWindowsServerConfig(windowsProfile, backupBeforeSave: true);
    AssertFileContains(windowsConfigPath, "ServerName=Smoke Server", "Windows server config");
    AssertFileContains(windowsConfigPath, "GamePort=7779", "Windows server config game port");
    File.AppendAllText(windowsConfigPath, "# old config marker");
    var rewrittenWindowsConfigPath = ServerManagerService.WriteWindowsServerConfig(windowsProfile, backupBeforeSave: true);
    AssertFileExists(rewrittenWindowsConfigPath, "rewritten Windows server config");
    if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(windowsConfigPath)!, "VeinManagerBackups")))
    {
        throw new InvalidOperationException("Windows config backup folder was not created.");
    }

    var injectedProfile = windowsProfile with { ServerName = "Good Server\nInjectedSetting=true" };
    var sanitizedConfigPath = ServerManagerService.WriteWindowsServerConfig(injectedProfile, backupBeforeSave: true);
    AssertFileContains(sanitizedConfigPath, "ServerName=Good Server InjectedSetting=true", "sanitized Windows server config");
    AssertFileDoesNotContain(sanitizedConfigPath, "\nInjectedSetting=true", "Windows server config newline injection prevention");

    var parityTemplateRoot = Path.GetDirectoryName(template)!;
    var paritySettings = new ModParitySettings(
        AllowExtraMods: false,
        EnforcementMode: "Log Only",
        KickMessage: "Smoke test modpack mismatch.");
    var invalidParityFolder = Path.Combine(testRoot, "RenamedButNotAMod");
    Directory.CreateDirectory(invalidParityFolder);
    var invalidParityResult = ModParityService.ValidateModFolder(invalidParityFolder);
    AssertEqual(false, invalidParityResult.IsValid, "invalid mod parity folder rejected");
    AssertThrowsContains(
        () => ModParityService.BuildManifest(new[] { invalidParityFolder }, paritySettings),
        "Scripts",
        "invalid mod parity manifest rejection");

    var parityPackage = ModParityService.ExportPackage(new[] { modFolder }, paritySettings, parityTemplateRoot);
    AssertFileExists(parityPackage.ZipPath, "mod parity package zip");
    AssertFileContains(Path.Combine(parityPackage.FolderPath, "expected_mods.json"), "ItemAndContainerModifier", "mod parity json manifest");
    AssertFileContains(Path.Combine(parityPackage.FolderPath, "expected_mods.json"), "main.lua", "mod parity per-file manifest");
    AssertFileContains(Path.Combine(parityPackage.FolderPath, "expected_mods.json"), "Sha256", "mod parity file hashes");
    AssertFileDoesNotContain(Path.Combine(parityPackage.FolderPath, "expected_mods.json"), testRoot, "mod parity local path omission");
    AssertFileContains(Path.Combine(parityPackage.FolderPath, "expected_mods.lua"), "allow_extra_mods = false", "mod parity lua manifest");
    AssertFileContains(Path.Combine(parityPackage.FolderPath, "expected_mods.lua"), "files = {", "mod parity lua file list");
    AssertFileContains(
        Path.Combine(parityPackage.FolderPath, "VeinManagerParityServer", "Scripts", "main.lua"),
        "VeinManagerParityServer",
        "server parity template");

    var installedParityPath = ModParityService.InstallWindowsServerMod(serverRoot, new[] { modFolder }, paritySettings, parityTemplateRoot);
    AssertFileContains(Path.Combine(installedParityPath, "Scripts", "expected_mods.lua"), "Smoke test modpack mismatch.", "installed server parity manifest");

    Console.WriteLine("VEIN Mod Manager smoke tests passed.");
}
finally
{
    if (Directory.Exists(testRoot))
    {
        Directory.Delete(testRoot, recursive: true);
    }
}

static void AssertFileDoesNotContain(string path, string unexpected, string label)
{
    AssertFileExists(path, label);
    var text = File.ReadAllText(path);
    if (text.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: did not expect to find {unexpected}");
    }
}

static void AssertThrowsContains(Action action, string expectedMessagePart, string label)
{
    try
    {
        action();
    }
    catch (Exception ex) when (ex.Message.Contains(expectedMessagePart, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    throw new InvalidOperationException($"{label}: expected exception containing {expectedMessagePart}");
}

static void AssertSequenceContains(IList<string> values, string[] expected, string label)
{
    for (var start = 0; start <= values.Count - expected.Length; start++)
    {
        var matches = true;
        for (var offset = 0; offset < expected.Length; offset++)
        {
            if (!string.Equals(values[start + offset], expected[offset], StringComparison.Ordinal))
            {
                matches = false;
                break;
            }
        }

        if (matches) return;
    }

    throw new InvalidOperationException($"{label}: expected sequence {string.Join(" ", expected)}");
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

static void AssertFileContains(string path, string expected, string label)
{
    AssertFileExists(path, label);
    var text = File.ReadAllText(path);
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected to find {expected}");
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
