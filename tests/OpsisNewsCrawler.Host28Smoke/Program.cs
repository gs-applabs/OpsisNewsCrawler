using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using OpsisNewsCrawler.Plugin.Abstractions;

// Tests the actual nested Opsis 2.8 PluginLoadContext, not a substitute mock loader.
if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: Host28Smoke <Opsis.dll> <OpsisNewsCrawler.OpsisPlugin.dll> [Chronikologio.OpsisPlugin.dll]");
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

// The Opsis host owns the process-wide Opsis.UI token adapter. Plugin
// construction, theme pushes and tray queries must not replace it or start a
// hidden TickerHost before Start() is called.
var owner = Opsis.UI.OpsisTheme.Current;
var plugin = (IOpsisNewsCrawlerPlugin)Activator.CreateInstance(plugins[0])!;
plugin.SetTheme(true, unchecked((int)0xff111111), unchecked((int)0xff222222),
    unchecked((int)0xff333333), unchecked((int)0xffffffff),
    unchecked((int)0xffaaaaaa), unchecked((int)0xff00bcd4), 0.94);
plugin.SetLanguage("el");
if (plugin is not IPluginHostSettingsAware hostAware)
    throw new InvalidDataException("Crawler must accept optional Opsis 2.8 host settings.");
hostAware.SetHostSettings(new PluginHostSettings("el", true, "Low", 125, false));
// Execute the *actual* Opsis 2.8 PluginHost P0 bridge (no mock).
var p0 = hostType.GetMethod("PushCrawlerP0Environment",
    BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new MissingMethodException("Opsis 2.8 host P0 shared-weather/shell bridge was not found.");
p0.Invoke(null, [plugin]);
if (!ReferenceEquals(owner, Opsis.UI.OpsisTheme.Current))
    throw new InvalidDataException("Crawler replaced the Opsis-owned shared UI6 theme.");
var controller = plugins[0].GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(plugin)
    ?? throw new MissingFieldException("Crawler controller not found");
var internalHostField = controller.GetType().GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new MissingFieldException("Crawler internal host field not found");
if (internalHostField.GetValue(controller) is not null)
    throw new InvalidDataException("Crawler created a hidden ticker before Opsis invoked Start.");
var hostState = controller.GetType().GetProperty("Context")?.GetValue(controller)
    ?? throw new MissingMemberException("Crawler P0 host context missing.");
var snapshot = hostState.GetType().GetProperty("Snapshot")?.GetValue(hostState)
    ?? throw new MissingMemberException("Crawler P0 snapshot missing.");
if (snapshot.GetType().GetProperty("SafeMode")?.GetValue(snapshot) is not bool)
    throw new InvalidDataException("Opsis P0 bridge did not deliver Safe Mode.");
if (controller.GetType().GetField("weatherProvider", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller)
    is not Func<double, double, Task<string>>)
    throw new InvalidDataException("Opsis cached WeatherHub callback was not provided.");
if (internalHostField.GetValue(controller) is not null)
    throw new InvalidDataException("Opsis P0 bridge unexpectedly created a Crawler ticker.");
plugin.GetTrayMenuItems();
if (internalHostField.GetValue(controller) is not null)
    throw new InvalidDataException("Fetching Opsis tray entries unexpectedly creates a ticker.");
if (plugin.Version != "0.30.1" || string.IsNullOrWhiteSpace(plugin.Id))
    throw new InvalidDataException("Unexpected product version or plugin ID.");
if (plugin is not IPluginWidgetProvider widgetProvider || widgetProvider.GetWidgets().Count != 5)
    throw new InvalidDataException("Five Opsis widgets are required.");
if (plugin.GetNavigationItems().Count == 0 || plugin.GetTrayMenuItems().Count == 0)
    throw new InvalidDataException("Plugin navigation or tray menu is missing.");

if (args.Length == 3)
{
    var chronPath = Path.GetFullPath(args[2]);
    if (!File.Exists(chronPath)) throw new FileNotFoundException("Chronikologio plugin missing", chronPath);
    var chronLoader = (AssemblyLoadContext)constructor.Invoke([chronPath]);
    var chronAssembly = chronLoader.LoadFromAssemblyPath(chronPath);
    var chronTypes = chronAssembly.GetTypes().Where(t =>
        !t.IsAbstract && typeof(IOpsisNewsCrawlerPlugin).IsAssignableFrom(t)).ToList();
    if (chronTypes.Count != 1)
        throw new InvalidDataException("Chronikologio no longer loads through the real Opsis 2.8 host contract.");
    var chron = (IOpsisNewsCrawlerPlugin)Activator.CreateInstance(chronTypes[0])!;
    if (chron is not IPluginWidgetProvider || chron is not IPluginHostSettingsAware chronAware)
        throw new InvalidDataException("Chronikologio optional Opsis 2.8 capabilities are missing.");
    chronAware.SetHostSettings(new PluginHostSettings("el", true, "Balanced", 100, true));
    if (!ReferenceEquals(owner, Opsis.UI.OpsisTheme.Current))
        throw new InvalidDataException("Chronikologio took ownership of the host theme.");
    Console.WriteLine("PASS: Chronikologio loads using the same shared ABI 1.0.0.0 and UI6.");
}
Console.WriteLine($"PASS: Opsis {hostVersion} loaded Crawler {plugin.Version}, ABI 1.0.0.0, UI 0.6.0, 5 widgets.");
return 0;
