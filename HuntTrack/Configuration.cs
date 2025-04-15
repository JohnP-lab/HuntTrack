﻿using System;
using Dalamud.Configuration;

namespace HuntTrack;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    //public bool IsConfigWindowMovable { get; set; } = true;
    
    public bool IsEnglish { get; set; } = false;
    //public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
