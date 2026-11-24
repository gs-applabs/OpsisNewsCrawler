using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using OpsisNewsCrawler.Plugin.Abstractions;

// Tests the actual nested Opsis 2.8 PluginLoadContext, not a substitute mock loader.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Host28Smoke <Opsis.dll> <OpsisNewsCrawler.OpsisPlugin.dll>");
    return 2;
}

var hostPath = Path.GetFullPath(args[0]);
var pluginPath = Path.GetFullPath(args[1]);
if (!File.Exists(hostPath) || !File.Exists(pluginPath))
    throw new FileNotFoundException("Missing host or plugin DLL.");

var hostVersion = FileVersionInfo.GetVersionInfo(hostPath).ProductVersion;
if (hostVersion is null || !hostVersion.StartsWith("2.8.", StringComparison.Ordinal))
    throw new InvalidDataException($"Expected Opsis 2.8.x host; found {hostVersion}");

var contract = typeof(IOpsisNewsCrawlerPlugin).Assembly;
if (contract.GetName().Version?.ToString() != "1.0.0.0")
    throw new InvalidDataException("Crawler contract is not ABI 1.0.0.0.");

var sharedUi = typeof(Opsis.UI.FlatButton).Assembly;
if (sharedUi.GetName().Version?.ToString() != "0.1.0.0" ||
    FileVersionInfo.GetVersionInfo(sharedUi.Location).FileVersion != "0.6.0.0")
    throw new InvalidDataException("Opsis.UI 0.6.0 is not the shared UI assembly.");

var hostDir = Path.GetDirectoryName(hostPath)!;
foreach (var (file, expected) in new[] {
    ("OpsisNewsCrawler.Plugin.Abstractions.dll", contract.Location),
    ("Opsis.UI.dll", sharedUi.Location)
})
{
    var installed = Path.Combine(hostDir, file);
    if (!File.Exists(installed) ||
        !File.ReadAllBytes(installed).AsSpan().SequenceEqual(File.ReadAllBytes(expected)))
        throw new InvalidDataException($"Host {file} must be byte-identical to the plugin's contract/UI6 DLL.");
}

var hostAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(hostPath);
var hostType = hostAssembly.GetType("MotionDesk.Services.PluginHost", throwOnError: true)!;
var loaderType = hostType.GetNestedType("PluginLoadContext", BindingFlags.NonPublic)
    ?? throw new MissingMemberException("Opsis 2.8 PluginHost.PluginLoadContext was not found.");
var constructor = loaderType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
    binder: null, types: [typeof(string)], modifiers: null)
    ?? throw new MissingMethodException("PluginLoadContext(string) was not found.");
var loader = (AssemblyLoadContext)constructor.Invoke([pluginPath]);
var pluginAssembly = loader.LoadFromAssemblyPath(pluginPath);
var plugins = pluginAssembly.GetTypes()
    .Where(t => !t.IsAbstract && typeof(IOpsisNewsCrawlerPlugin).IsAssignableFrom(t)).ToList();
if (plugins.Count != 1)
    throw new InvalidDataException($"Expected one compatible Crawler plugin; found {plugins.Count}.");

var plugin = (IOpsisNewsCrawlerPlugin)Activator.CreateInstance(plugins[0])!;
if (plugin.Version != "0.28.0" || string.IsNullOrWhiteSpace(plugin.Id))
    throw new InvalidDataException("Unexpected product version or plugin ID.");
if (plugin is not IPluginWidgetProvider widgetProvider || widgetProvider.GetWidgets().Count != 5)
    throw new InvalidDataException("Five Opsis widgets are required.");
if (plugin.GetNavigationItems().Count == 0 || plugin.GetTrayMenuItems().Count == 0)
    throw new InvalidDataException("Plugin navigation or tray menu is missing.");

Console.WriteLine($"PASS: Opsis {hostVersion} loaded Crawler {plugin.Version}, ABI 1.0.0.0, UI 0.6.0, 5 widgets.");
return 0;
