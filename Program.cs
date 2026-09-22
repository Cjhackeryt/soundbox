using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;
using SoundBox;

var plugin = MacroDeckPlugin.CreatePlugin(args)
    .UseMacroDeckLogging()
    .RegisterIntegration<PluginIntegration>()
    .Build();

await plugin.RunAsync();
