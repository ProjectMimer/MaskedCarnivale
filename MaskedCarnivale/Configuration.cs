using Dalamud.Configuration;
using System;

namespace MaskedCarnivale;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool enable { get; set; } = false;
    public bool showUI { get; set; } = true;
    public int xPosition { get; set; } = 0;
    public int yPosition { get; set; } = 0;
    public int renderIndex { get; set; } = 0;
    public int orderStatus { get; set; } = 1;

    public bool doUpdate { get; set; } = false;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
