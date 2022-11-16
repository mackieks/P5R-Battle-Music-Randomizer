using System.ComponentModel;
using BattleMusicRandomizer.Template.Configuration;

namespace BattleMusicRandomizer.Configuration;

public class Config : Configurable<Config>
{
    /*
        User Properties:
            - Please put all of your configurable properties here.
    
        By default, configuration saves as "Config.json" in mod user config folder.    
        Need more config files/classes? See Configuration.cs
    
        Available Attributes:
        - Category
        - DisplayName
        - Description
        - DefaultValue

        // Technically Supported but not Useful
        - Browsable
        - Localizable

        The `DefaultValue` attribute is used as part of the `Reset` button in Reloaded-Launcher.
    */

    [DisplayName("String")]
    [Description("This is a string.")]
    [DefaultValue("Default Name")]
    public string String { get; set; } = "Default Name";
    //Not Implemented
    /*
    [DisplayName("Ambush BGM Cue ID ")]
    [Description("The Cue ID corresponding to the BGM track to play when engadging in normal enemy ambushes")]
    [DefaultValue(300)]
    public int AmbushBgmId { get; set; } = 300;
    [DisplayName("Ambush BGM Cue ID ")]
    [Description("The Cue ID corresponding to the BGM track to play when engadging in normal enemy ambushes")]
    [DefaultValue(907)]
    */
    public int BattleBgmId { get; set; } = 907;

    [DisplayName("Bool")]
    [Description("This is a bool.")]
    [DefaultValue(true)]
    public bool Boolean { get; set; } = true;

    [DisplayName("Float")]
    [Description("This is a floating point number.")]
    [DefaultValue(6.987654F)]
    public float Float { get; set; } = 6.987654F;

    [DisplayName("Enum")]
    [Description("This is an enumerable.")]
    [DefaultValue(SampleEnum.ILoveIt)]
    public SampleEnum Reloaded { get; set; } = SampleEnum.ILoveIt;

    public enum SampleEnum
    {
        NoOpinion,
        Sucks,
        IsMediocre,
        IsOk,
        IsCool,
        ILoveIt
    }
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    // 
}
