using System.Text.Json;
using System.Text.Json.Serialization;

namespace QMKMacroService.Config;

public record MacroConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public required MacroLayout DefaultLayout { get; set; }
    public required Dictionary<string, MacroLayout> ApplicationLayouts { get; set; }

    public static MacroConfig Default()
    {
        return new MacroConfig
        {
            DefaultLayout = MacroLayout.Default(),
            ApplicationLayouts = new Dictionary<string, MacroLayout>()
        };
    }

    public static async Task<MacroConfig> GetMacroConfig(string filepath, CancellationToken stoppingToken)
    {
        var jsonString = await File.ReadAllTextAsync(filepath, stoppingToken);
        var macroConfigNullable = JsonSerializer.Deserialize<MacroConfig>(jsonString, SerializerOptions);

        return macroConfigNullable ??
               throw new NullReferenceException("JSON Deserialization resulted in a null MacroConfig.");
    }

    public static async Task<MacroConfig> SaveDefaultMacroConfig(string filepath, CancellationToken stoppingToken)
    {
        JsonSerializerOptions serializerOptions;
        var macroConfig = MacroConfig.Default();

        var jsonString = JsonSerializer.Serialize(macroConfig, SerializerOptions);

        File.Create(filepath).Close();
        await File.WriteAllTextAsync(filepath, jsonString, stoppingToken);

        return macroConfig;
    }
}

public record MacroLayout
{
    public required Dictionary<byte, Macro> Macros { get; set; }

    public static MacroLayout Default()
    {
        return new MacroLayout
        {
            Macros = new Dictionary<byte, Macro>()
            {
                {
                    1, new Macro
                    {
                        Colour = default,
                        KeyActions = new List<InputApi.KeyAction>()
                        {
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.CONTROL,
                                Event = InputApi.KeyEventF.KEYDOWN
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.KEY_C,
                                Event = InputApi.KeyEventF.KEYDOWN
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.CONTROL,
                                Event = InputApi.KeyEventF.KEYUP
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.KEY_C,
                                Event = InputApi.KeyEventF.KEYUP
                            }
                        }
                    }
                },
                {
                    2, new Macro
                    {
                        Colour = default,
                        KeyActions = new List<InputApi.KeyAction>()
                        {
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.CONTROL,
                                Event = InputApi.KeyEventF.KEYDOWN
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.KEY_V,
                                Event = InputApi.KeyEventF.KEYDOWN
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.CONTROL,
                                Event = InputApi.KeyEventF.KEYUP
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.KEY_V,
                                Event = InputApi.KeyEventF.KEYUP
                            }
                        }
                    }
                },
                {
                    3, new Macro
                    {
                        Colour = default,
                        KeyActions = new List<InputApi.KeyAction>()
                        {
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.LWIN,
                                Event = InputApi.KeyEventF.KEYDOWN
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.SHIFT,
                                Event = InputApi.KeyEventF.KEYDOWN
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.KEY_S,
                                Event = InputApi.KeyEventF.KEYDOWN
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.LWIN,
                                Event = InputApi.KeyEventF.KEYUP
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.SHIFT,
                                Event = InputApi.KeyEventF.KEYUP
                            },
                            new()
                            {
                                Keycode = InputApi.VirtualKeyShort.KEY_S,
                                Event = InputApi.KeyEventF.KEYUP
                            }
                        }
                    }
                }
            }
        };
    }
}

public record Macro
{
    public required Colour Colour { get; set; }
    public required IList<InputApi.KeyAction> KeyActions { get; set; }
}

public struct Colour
{
    public required byte Red { get; set; }
    public required byte Green { get; set; }
    public required byte Blue { get; set; }
}