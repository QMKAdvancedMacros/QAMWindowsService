using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using HidSharp;
using HidSharp.Reports;
using QMKMacroService.Config;

namespace QMKMacroService;

public class Worker : BackgroundService
{
    private readonly string _configFolder;
    private readonly string _configPath;
    
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configFolder = Path.Combine(appDataFolder, "QMKMacroService");
        _configPath = Path.Combine(_configFolder, "MacroConfig.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var macroConfig = await GetOrCreateMacroConfig(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var macroPadApi = await ConnectMacroPad(stoppingToken);

                await CyclicExecutive(stoppingToken, macroConfig, macroPadApi);
                
                if (stoppingToken.IsCancellationRequested) continue;
                
                _logger.LogWarning("Cyclic executive finished execution. Restarting execution.");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (TaskCanceledException e)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogError("Cyclic executive restarting: {error}", e.Message);
            }
        }
    }

    private static async Task CyclicExecutive(CancellationToken stoppingToken, MacroConfig macroConfig,
        MacroPadApi macroPadApi)
    {
        string? currentApplication = null;
        MacroLayout currentLayout = macroConfig.DefaultLayout;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            UpdateCurrentApplication(macroConfig, macroPadApi, ref currentApplication, ref currentLayout);

            var commandKeyPresses = macroPadApi.GetCommandKeyPresses();

            foreach (var commandKeyPress in commandKeyPresses)
            {
                if (currentLayout.Macros.TryGetValue(commandKeyPress, out var macro))
                {
                    InputApi.SendKeyActions(macro.KeyActions);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);
        }
    }

    private static void UpdateCurrentApplication(MacroConfig macroConfig, MacroPadApi macroPadApi, ref string? currentApplication,
        ref MacroLayout currentLayout)
    {
        var application = WindowApi.GetCurrentWindowName();
        
        if (string.Equals(currentApplication, application)) return;
        
        currentApplication = application;
        var layout = GetMacroLayout(currentApplication, macroConfig);

        if (currentLayout == layout) return;
        
        currentLayout = layout;
                    
        var packet = new List<byte>();
        foreach (var keyValuePair in currentLayout.Macros)
        {
            packet.Add((byte)(0b10000000 | keyValuePair.Key));
            packet.Add(keyValuePair.Value.Colour.Red);
            packet.Add(keyValuePair.Value.Colour.Green);
            packet.Add(keyValuePair.Value.Colour.Blue);
        }
                
        macroPadApi.WriteToRawHid([255]);
        macroPadApi.WriteToRawHid(packet);
    }

    private static MacroLayout GetMacroLayout(string? application, MacroConfig macroConfig)
    {
        return (application is null
            ? macroConfig.DefaultLayout
            : macroConfig.ApplicationLayouts
                .GetValueOrDefault(application, macroConfig.DefaultLayout));
    }

    private async Task<MacroConfig> GetOrCreateMacroConfig(CancellationToken stoppingToken)
    {
        MacroConfig macroConfig;
        if (!Directory.Exists(_configFolder))
        {
            Directory.CreateDirectory(_configFolder);
        }
        
        if (!File.Exists(_configPath))
        {
            macroConfig = await MacroConfig.SaveDefaultMacroConfig(_configPath, stoppingToken);
        }
        else
        {
            macroConfig = await MacroConfig.GetMacroConfig(_configPath, stoppingToken);
        }

        return macroConfig;
    }

    private async Task<MacroPadApi> ConnectMacroPad(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                return new MacroPadApi(_logger);
            }
            catch (Exception e)
            {
                _logger.LogError("Unable to connect to device: {message}", e.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        return new MacroPadApi(_logger);
    }
}