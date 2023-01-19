using Reloaded.Mod.Interfaces;
using BattleMusicRandomizer.Template;
using BattleMusicRandomizer.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Reloaded.Assembler;
using Reloaded.Hooks.Internal;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.X64;
using Reloaded.Hooks.X86;
using Reloaded.Hooks.Tests.Shared.Macros;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Definitions.Structs;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using System.Numerics;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;
using System.Runtime.InteropServices;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Imgui.Hook;
using Reloaded.Imgui.Hook.Direct3D11;
using DearImguiSharp;
using SharpConfig;
using System.Reflection;
using Reloaded.Hooks.Definitions.X64;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Windows.Forms;
using System.Drawing;
using System;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace BattleMusicRandomizer;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private IReverseWrapper<ambushBGMDelegate> _ambushBGMReverseWrapper;
    private IReverseWrapper<battleBGMDelegate> _battleBGMReverseWrapper;
    private IReverseWrapper<resultsBGMDelegate> _resultsBGMReverseWrapper;

    private IAsmHook _battleBGMhook;
    private IAsmHook _resultsBGMhook;

    private static bool[] ambushTracks = new bool[26];
    private static bool[] battleTracks = new bool[26];
    private static bool[] resultsTracks = new bool[12];

    private static int[] fAmbushTracks = new int[26];
    private static int[] fBattleTracks = new int[26];
    private static int[] fResultsTracks = new int[12];

    private static bool showWindow = false;

    private static int ambushTrackCount = 0;
    private static int battleTrackCount = 0;
    private static int resultsTrackCount = 0;

    private static int currentAmbushTrack = 0;
    private static int currentBattleTrack = 0;
    private static int currentResultsTrack = 0;

    private static int ambushBGMid = 0;

    private static bool tracksUpdated = true;
    private static volatile int ambushCueID = 0;

    long ms = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);

    public Mod(ModContext context)
    {

        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;

        _logger.Write($"BGM Randomizer v2.0.0 by YveltalGriffin\n", _logger.ColorPinkLight);
        _logger.Write($"Loading p5rbgm.cfg...\n", _logger.ColorPinkLight);

        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string cfgLocation = Path.Combine(assemblyFolder, "p5rbgm.cfg");
        var config = SharpConfig.Configuration.LoadFromFile(cfgLocation);
        
        var section = config["General"];
        ambushTracks = section["ambushTracks"].BoolValueArray;
        battleTracks = section["battleTracks"].BoolValueArray;
        resultsTracks = section["resultsTracks"].BoolValueArray;

        Memory memory = Memory.Instance;
        using Process thisProcess = Process.GetCurrentProcess();
        long baseAddress = thisProcess.MainModule!.BaseAddress.ToInt64();
        _modLoader.GetController<IStartupScanner>().TryGetTarget(out var startupScanner);

        startupScanner.AddMainModuleScan("C7 08 83 FE 11 72 D9 EB 0C", delegate (PatternScanResult result)
        {

            long num = result.Offset + baseAddress;

            if (result.Found)
            {
                _logger.Write($"Found DLC BGM ACB code!\n", _logger.ColorPinkLight);
                memory.SafeWrite(num, 0xEBD97211FE8308C7, marshal: false);
                memory.SafeWrite(num + 8, 0x0141B3A498BF4800, marshal: false);
                memory.SafeWrite(num + 16, 0x9090909090000000, marshal: false);
                _logger.Write($"Wrote DLC BGM ACB change stub to memory!\n", _logger.ColorPinkLight);
            }
            else
            {
                _logger.Write($"Couldn't find DLC BGM ACB code!\n", _logger.ColorPinkLight);
            }
        });

        startupScanner.AddMainModuleScan("83 E8 01 74 0A BA 8B 03 00 00 83 F8 01", delegate (PatternScanResult result)
        {

            long num = result.Offset + baseAddress;

            if (result.Found)
            {
                _logger.Write($"Found battle theme code!\n", _logger.ColorPinkLight);
                
                string[] function =
                {
                    $"use64",
                    $"test eax, eax",
                    $"je battlethemeshuffle",
                    $"push rax\npush rcx\npush r8\npush r10",
                    $"sub rsp, 32",
                    $"{_hooks.Utilities.GetAbsoluteCallMnemonics(ambushBGM, out _ambushBGMReverseWrapper)}",
                    $"add rsp, 32",
                    $"pop r10\npop r8\npop rcx\npop rax",
                    $"mov rax, {num + 20}",
                    $"jmp rax",
                    $"battlethemeshuffle:",
                    $"push rax\npush rcx\npush r8\npush r10",
                    $"sub rsp, 32",
                    $"{_hooks.Utilities.GetAbsoluteCallMnemonics(battleBGM, out _battleBGMReverseWrapper)}",
                    $"add rsp, 32",
                    $"pop r10\npop r8\npop rcx\npop rax",
                    $"mov rax, {num + 20}",
                    $"jmp rax",
                };
                _battleBGMhook = _hooks.CreateAsmHook(function, num, AsmHookBehaviour.DoNotExecuteOriginal).Activate();

                _logger.Write($"Wrote battle theme patch to memory!\n", _logger.ColorPinkLight);
            }
            else
            {
                _logger.Write($"Couldn't find battle theme address!\n", _logger.ColorPinkLight);
            }

        });

        startupScanner.AddMainModuleScan("BA 54 01 00 00 48 8B CE", delegate (PatternScanResult result)
        {

            long num = result.Offset + baseAddress;

            if (result.Found)
            {
                _logger.Write($"Found results theme code!\n", _logger.ColorPinkLight);
                string[] function =
                {
                    $"use64",
                    $"push rax\npush rcx\npush r8\npush r10",
                    $"sub rsp, 32",
                    $"{_hooks.Utilities.GetAbsoluteCallMnemonics(resultsBGM, out _resultsBGMReverseWrapper)}",
                    $"add rsp, 32",
                    $"pop r10\npop r8\npop rcx\npop rax",
                    $"mov r14, {num + 8}",
                    $"mov rcx, rsi",
                    $"jmp r14",
                };
                _resultsBGMhook = _hooks.CreateAsmHook(function, num, AsmHookBehaviour.DoNotExecuteOriginal).Activate();

                _logger.Write($"Wrote results theme patch to memory!\n", _logger.ColorPinkLight);
            }
            else
            {
                _logger.Write($"Couldn't find results theme address!\n", _logger.ColorPinkLight);
            }

        });

        tracksUpdated = true;
        _logger.Write("Tracklist updated!\n", _logger.ColorPinkLight);

        // Update ambush tracklist

        ambushTrackCount = 0;

        for (int i = 0; i < ambushTracks.Length; i++)
        {
            if (ambushTracks[i] == true)
            {
                ambushTrackCount++;

            }
        }

        //_logger.Write("Number of selected ambush tracks:" + ambushTrackCount + "\n");

        //_logger.Write("Length of new fAmbushTracks array:" + fAmbushTracks.Length + "\n");

        int j = 0;

        for (int i = 0; i < fAmbushTracks.Length; i++)
        {
            if (ambushTracks[i] == true)
            {
                fAmbushTracks[j] = 962 + i;
                //_logger.Write("fAmbushTrack No. " + j + ": " + fAmbushTracks[j] + "\n");
                j++;
            }
        }

        Shuffle(ambushTrackCount, fAmbushTracks);

        for (int i = 0; i < ambushTrackCount; i++)
        {
            //_logger.Write("fAmbushTrack No. " + i + ": " + fAmbushTracks[i] + "\n");
        }

        currentAmbushTrack = 0;

        // Update battle tracklist

        battleTrackCount = 0;

        for (int i = 0; i < battleTracks.Length; i++)
        {
            if (battleTracks[i] == true)
            {
                battleTrackCount++;

            }
        }

        //_logger.Write("Number of selected battle tracks:" + battleTrackCount + "\n");

        //_logger.Write("Length of new fBattleTracks array:" + fBattleTracks.Length + "\n");

        j = 0;

        for (int i = 0; i < fBattleTracks.Length; i++)
        {
            if (battleTracks[i] == true)
            {
                fBattleTracks[j] = 962 + i;
                //_logger.Write("fBattleTrack No. " + j + ": " + fBattleTracks[j] + "\n");
                j++;
            }
        }

        Shuffle(battleTrackCount, fBattleTracks);

        for (int i = 0; i < battleTrackCount; i++)
        {
            //_logger.Write("fBattleTrack No. " + i + ": " + fBattleTracks[i] + "\n");
        }

        currentBattleTrack = 0;

        // Update results tracklist

        resultsTrackCount = 0;

        for (int i = 0; i < resultsTracks.Length; i++)
        {
            if (resultsTracks[i] == true)
            {
                resultsTrackCount++;

            }
        }

        //_logger.Write("Number of selected results tracks:" + resultsTrackCount + "\n");

        //_logger.Write("Length of new fResultsTracks array:" + fResultsTracks.Length + "\n");

        j = 0;

        for (int i = 0; i < fResultsTracks.Length; i++)
        {
            if (resultsTracks[i] == true)
            {
                fResultsTracks[j] = 988 + i;
                //_logger.Write("fResultsTrack No. " + j + ": " + fResultsTracks[j] + "\n");
                j++;
            }
        }

        Shuffle(resultsTrackCount, fResultsTracks);

        for (int i = 0; i < resultsTrackCount; i++)
        {
            //_logger.Write("fResultsTrack No. " + i + ": " + fResultsTracks[i] + "\n");
        }

        currentResultsTrack = 0;


        tracksUpdated = false;

        config["General"]["ambushTracks"].BoolValueArray = ambushTracks;
        config["General"]["battleTracks"].BoolValueArray = battleTracks;
        config["General"]["resultsTracks"].BoolValueArray = resultsTracks;
        config.SaveToFile(cfgLocation);


        Start(_modLoader, _hooks, _logger);

    }

    private int ambushBGM()
    {
        
        if (currentAmbushTrack == ambushTrackCount)
        {
            if(ambushTrackCount > 2)
                Shuffle(ambushTrackCount, fAmbushTracks);
            currentAmbushTrack = 0;
        }

        //_logger.Write("currentAmbushTrack = " + currentAmbushTrack + "\n");
        ambushBGMid = fAmbushTracks[currentAmbushTrack];
        //_logger.Write("ambushBGMid = " + ambushBGMid + "\n");
        currentAmbushTrack++;

        return ambushBGMid;
        
    }

    private int battleBGM()
    {
        if (currentBattleTrack == battleTrackCount)
        {
            if (battleTrackCount > 2)
                Shuffle(battleTrackCount, fBattleTracks);
            currentBattleTrack = 0;
        }

        //_logger.Write("currentBattleTrack = " + currentBattleTrack + "\n");
        int battleBGMid = fBattleTracks[currentBattleTrack];
        //_logger.Write("battleBGMid = " + battleBGMid + "\n");
        currentBattleTrack++;

        return battleBGMid;

    }

    private int resultsBGM()
    {

        if (currentResultsTrack == resultsTrackCount)
        {
            if (resultsTrackCount > 2)
                Shuffle(resultsTrackCount, fAmbushTracks);
            currentResultsTrack = 0;
        }

        //_logger.Write("currentResultsTrack = " + currentResultsTrack + "\n");
        int resultsBGMid = fResultsTracks[currentResultsTrack];
        //_logger.Write("resultsBGMid = " + resultsBGMid + "\n");
        currentResultsTrack++;

        return resultsBGMid;

    }

    public async void Start(IModLoaderV1 loader, IReloadedHooks hooks, ILogger _logger)
    {

        loader.GetController<IReloadedHooks>().TryGetTarget(out hooks);

        SDK.Init(hooks);
        await ImguiHook.Create(RenderTestWindow, new ImguiHookOptions()
        {
            EnableViewports = true,
            IgnoreWindowUnactivate = true,
            Implementations = new List<IImguiHook>()
                {
                    new ImguiHookDx11()
                }
        }).ConfigureAwait(false);
    }

    public unsafe void RenderTestWindow()
    {

        DearImguiSharp.ImGuiViewport main_viewport = DearImguiSharp.ImGui.GetMainViewport();
        DearImguiSharp.ImVec2 windowpos = new ImVec2() { X = main_viewport.WorkPos.X + 650, Y = main_viewport.WorkPos.Y + 20 };
        DearImguiSharp.ImGui.SetNextWindowSize(windowpos, 1 << 2);
        DearImguiSharp.ImGui.SetNextWindowBgAlpha(0.45f);

        

        var style = DearImguiSharp.ImGui.GetStyle();
        style.WindowRounding = 5.0f;
        var colors = style.Colors;
        colors[(int)DearImguiSharp.ImGuiCol.Text] = new ImVec4() { X = 255 / 255f, Y = 255 / 255f, Z = 255 / 255f, W = 221 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.TitleBgActive] = new ImVec4() { X = 150 / 255f, Y = 0f, Z = 0f, W = 221 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.TitleBg] = new ImVec4() { X = 150 / 255f, Y = 0f, Z = 0f, W = 95 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.TitleBgCollapsed] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 95 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.MenuBarBg] = new ImVec4() { X = 250 / 255f, Y = 0f, Z = 0f, W = 100 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ScrollbarGrab] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 115 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ScrollbarGrabHovered] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 141 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ScrollbarGrabActive] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 162 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.Header] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 126 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.HeaderHovered] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 156 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.HeaderActive] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 176 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ResizeGrip] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 60 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ResizeGripHovered] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 80 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ResizeGripActive] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 100 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.FrameBg] = new ImVec4() { X = 74 / 255f, Y = 74 / 255f, Z = 74 / 255f, W = 255 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.FrameBgHovered] = new ImVec4() { X = 204 / 255f, Y = 204 / 255f, Z = 204 / 255f, W = 102 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.FrameBgActive] = new ImVec4() { X = 204 / 255f, Y = 204 / 255f, Z = 204 / 255f, W = 102 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.CheckMark] = new ImVec4() { X = 226 / 255f, Y = 0 / 255f, Z = 0 / 255f, W = 185 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.Button] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 115 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ButtonHovered] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 141 / 255f };
        colors[(int)DearImguiSharp.ImGuiCol.ButtonActive] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 162 / 255f };

        style.Colors = colors;

        if (DearImguiSharp.ImGui.GetIO().KeyAlt)
            if( ( (Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000) ) - ms) > 250)
            {
                showWindow = !showWindow;
                ms = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
            }
                

        if (showWindow)
        {
            
            if (!DearImguiSharp.ImGui.Begin("Battle Music Randomizer", ref showWindow, 0))
            {
                DearImguiSharp.ImGui.End();
            }
            else
            {
                DearImguiSharp.ImGui.Text("Enable the songs you'd like to hear\nand press Apply to save your changes");
                DearImguiSharp.ImGui.SameLine(0,25);
                if (DearImguiSharp.ImGui.Button("Apply", new ImVec2() { X = 60, Y = 20 }))
                {
                    tracksUpdated = true;
                    _logger.Write("Tracklist updated!\n", _logger.ColorPinkLight);

                    // Update ambush tracklist

                    ambushTrackCount = 0;

                    for (int i = 0; i < ambushTracks.Length; i++)
                    {
                        if (ambushTracks[i] == true)
                        {
                            ambushTrackCount++;

                        }
                    }

                    //_logger.Write("Number of selected ambush tracks:" + ambushTrackCount + "\n");

                    //_logger.Write("Length of new fAmbushTracks array:" + fAmbushTracks.Length + "\n");

                    int j = 0;

                    for (int i = 0; i < fAmbushTracks.Length; i++)
                    {
                        if (ambushTracks[i] == true)
                        {
                            fAmbushTracks[j] = 962 + i;
                            //_logger.Write("fAmbushTrack No. " + j + ": " + fAmbushTracks[j] + "\n");
                            j++;
                        }
                    }

                    Shuffle(ambushTrackCount, fAmbushTracks);

                    for (int i = 0; i < ambushTrackCount; i++)
                    {
                        //_logger.Write("fAmbushTrack No. " + i + ": " + fAmbushTracks[i] + "\n");
                    }

                    currentAmbushTrack = 0;

                    // Update battle tracklist

                    battleTrackCount = 0;

                    for (int i = 0; i < battleTracks.Length; i++)
                    {
                        if (battleTracks[i] == true)
                        {
                            battleTrackCount++;

                        }
                    }

                    //_logger.Write("Number of selected battle tracks:" + battleTrackCount + "\n");

                    //_logger.Write("Length of new fBattleTracks array:" + fBattleTracks.Length + "\n");

                    j = 0;

                    for (int i = 0; i < fBattleTracks.Length; i++)
                    {
                        if (battleTracks[i] == true)
                        {
                            fBattleTracks[j] = 962 + i;
                            //_logger.Write("fBattleTrack No. " + j + ": " + fBattleTracks[j] + "\n");
                            j++;
                        }
                    }

                    Shuffle(battleTrackCount, fBattleTracks);

                    for (int i = 0; i < battleTrackCount; i++)
                    {
                        //_logger.Write("fBattleTrack No. " + i + ": " + fBattleTracks[i] + "\n");
                    }

                    currentBattleTrack = 0;

                    // Update results tracklist

                    resultsTrackCount = 0;

                    for (int i = 0; i < resultsTracks.Length; i++)
                    {
                        if (resultsTracks[i] == true)
                        {
                            resultsTrackCount++;

                        }
                    }

                    //_logger.Write("Number of selected results tracks:" + resultsTrackCount + "\n");

                    //_logger.Write("Length of new fResultsTracks array:" + fResultsTracks.Length + "\n");

                    j = 0;

                    for (int i = 0; i < fResultsTracks.Length; i++)
                    {
                        if (resultsTracks[i] == true)
                        {
                            fResultsTracks[j] = 988 + i;
                            //_logger.Write("fResultsTrack No. " + j + ": " + fResultsTracks[j] + "\n");
                            j++;
                        }
                    }

                    Shuffle(resultsTrackCount, fResultsTracks);

                    for (int i = 0; i < resultsTrackCount; i++)
                    {
                        //_logger.Write("fResultsTrack No. " + i + ": " + fResultsTracks[i] + "\n");
                    }

                    currentResultsTrack = 0;


                    tracksUpdated = false;

                    var config = new SharpConfig.Configuration();
                    string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string cfgLocation = Path.Combine(assemblyFolder, "p5rbgm.cfg");
                    config["General"]["ambushTracks"].BoolValueArray = ambushTracks;
                    config["General"]["battleTracks"].BoolValueArray = battleTracks;
                    config["General"]["resultsTracks"].BoolValueArray = resultsTracks;
                    config.SaveToFile(cfgLocation);
                }

                if (DearImguiSharp.ImGui.CollapsingHeaderTreeNodeFlags("Ambush Themes", 0))
                {
                    DearImguiSharp.ImGui.Checkbox("Take Over (P5R)", ref ambushTracks[0]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (P5)", ref ambushTracks[1]);
                    DearImguiSharp.ImGui.Checkbox("Mass Destruction (P3)", ref ambushTracks[2]);
                    DearImguiSharp.ImGui.Checkbox("Time To Make History (P4G)", ref ambushTracks[3]);
                    DearImguiSharp.ImGui.Checkbox("A Lone Prayer (Persona)", ref ambushTracks[4]);
                    DearImguiSharp.ImGui.Checkbox("Normal Battle (P2)", ref ambushTracks[5]);
                    DearImguiSharp.ImGui.Checkbox("Obelisk (Catherine)", ref ambushTracks[6]);
                    DearImguiSharp.ImGui.Checkbox("Reach Out to the Truth (P4D)", ref ambushTracks[7]);
                    DearImguiSharp.ImGui.Checkbox("The Ultimate (P4AU)", ref ambushTracks[8]);
                    DearImguiSharp.ImGui.Checkbox("Invitation to Freedom (PQ2)", ref ambushTracks[9]);
                    DearImguiSharp.ImGui.Checkbox("Axe to Grind (P5S)", ref ambushTracks[10]);
                    DearImguiSharp.ImGui.Checkbox("The Whims of Fate (Yukihiro Fukutomi Remix) (P5D)", ref ambushTracks[11]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (Taku Takahashi Remix) (P5D)", ref ambushTracks[12]);
                    DearImguiSharp.ImGui.Checkbox("Life Will Change Remix (ATLUS Meguro Remix) (P5D)", ref ambushTracks[13]);
                    DearImguiSharp.ImGui.Checkbox("Rivers in the Desert (Mito Remix) (P5D)", ref ambushTracks[14]);
                    DearImguiSharp.ImGui.Checkbox("What You Wish For (P5S)", ref ambushTracks[15]);
                    DearImguiSharp.ImGui.Checkbox("Groovy (P5D)", ref ambushTracks[16]);
                    DearImguiSharp.ImGui.Checkbox("Wiping All Out (P3P)", ref ambushTracks[17]);
                    DearImguiSharp.ImGui.Checkbox("Prison Labor (P5R)", ref ambushTracks[18]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (P5S)", ref ambushTracks[19]);
                    DearImguiSharp.ImGui.Checkbox("Reach Out to the Truth (Mayonaka Arena) (P4AU)", ref ambushTracks[20]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (Kirara Remix)", ref ambushTracks[21]);
                    DearImguiSharp.ImGui.Checkbox("Yo (Acid Jazz Ver.) (Catherine: Full Body)", ref ambushTracks[22]);
                    DearImguiSharp.ImGui.Checkbox("Old Enemy (SMT If...)", ref ambushTracks[23]);
                    DearImguiSharp.ImGui.Checkbox("Pull the Trigger (PQ2)", ref ambushTracks[24]);
                    DearImguiSharp.ImGui.Checkbox("Light Up the Fire in the Night (Dark Hour) (P3D)", ref ambushTracks[25]);

                }
                if (DearImguiSharp.ImGui.CollapsingHeaderTreeNodeFlags("Battle Themes", 0))
                {
                    DearImguiSharp.ImGui.Checkbox("Take Over (P5R)", ref battleTracks[0]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (P5)", ref battleTracks[1]);
                    DearImguiSharp.ImGui.Checkbox("Mass Destruction (P3)", ref battleTracks[2]);
                    DearImguiSharp.ImGui.Checkbox("Time To Make History (P4G)", ref battleTracks[3]);
                    DearImguiSharp.ImGui.Checkbox("A Lone Prayer (Persona)", ref battleTracks[4]);
                    DearImguiSharp.ImGui.Checkbox("Normal Battle (P2)", ref battleTracks[5]);
                    DearImguiSharp.ImGui.Checkbox("Obelisk (Catherine)", ref battleTracks[6]);
                    DearImguiSharp.ImGui.Checkbox("Reach Out to the Truth (P4D)", ref battleTracks[7]);
                    DearImguiSharp.ImGui.Checkbox("The Ultimate (P4AU)", ref battleTracks[8]);
                    DearImguiSharp.ImGui.Checkbox("Invitation to Freedom (PQ2)", ref battleTracks[9]);
                    DearImguiSharp.ImGui.Checkbox("Axe to Grind (P5S)", ref battleTracks[10]);
                    DearImguiSharp.ImGui.Checkbox("The Whims of Fate (Yukihiro Fukutomi Remix) (P5D)", ref battleTracks[11]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (Taku Takahashi Remix) (P5D)", ref battleTracks[12]);
                    DearImguiSharp.ImGui.Checkbox("Life Will Change Remix (ATLUS Meguro Remix) (P5D)", ref battleTracks[13]);
                    DearImguiSharp.ImGui.Checkbox("Rivers in the Desert (Mito Remix) (P5D)", ref battleTracks[14]);
                    DearImguiSharp.ImGui.Checkbox("What You Wish For (P5S)", ref battleTracks[15]);
                    DearImguiSharp.ImGui.Checkbox("Groovy (P5D)", ref battleTracks[16]);
                    DearImguiSharp.ImGui.Checkbox("Wiping All Out (P3P)", ref battleTracks[17]);
                    DearImguiSharp.ImGui.Checkbox("Prison Labor (P5R)", ref battleTracks[18]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (P5S)", ref battleTracks[19]);
                    DearImguiSharp.ImGui.Checkbox("Reach Out to the Truth (Mayonaka Arena) (P4AU)", ref battleTracks[20]);
                    DearImguiSharp.ImGui.Checkbox("Last Surprise (Kirara Remix)", ref battleTracks[21]);
                    DearImguiSharp.ImGui.Checkbox("Yo (Acid Jazz Ver.) (Catherine: Full Body)", ref battleTracks[22]);
                    DearImguiSharp.ImGui.Checkbox("Old Enemy (SMT If...)", ref battleTracks[23]);
                    DearImguiSharp.ImGui.Checkbox("Pull the Trigger (PQ2)", ref battleTracks[24]);
                    DearImguiSharp.ImGui.Checkbox("Light Up the Fire in the Night (Dark Hour) (P3D)", ref battleTracks[25]);
                }
                if (DearImguiSharp.ImGui.CollapsingHeaderTreeNodeFlags("Results Themes", 0))
                {
                    DearImguiSharp.ImGui.Checkbox("Triumph (P5)", ref resultsTracks[0]);
                    DearImguiSharp.ImGui.Checkbox("After the Battle (P3)", ref resultsTracks[1]);
                    DearImguiSharp.ImGui.Checkbox("Period (P4)", ref resultsTracks[2]);
                    DearImguiSharp.ImGui.Checkbox("Dream of a Butterfly (Persona)", ref resultsTracks[3]);
                    DearImguiSharp.ImGui.Checkbox("Battle Results (P2)", ref resultsTracks[4]);
                    DearImguiSharp.ImGui.Checkbox("Results (SMT If...)", ref resultsTracks[5]);
                    DearImguiSharp.ImGui.Checkbox("Period (P4D)", ref resultsTracks[6]);
                    DearImguiSharp.ImGui.Checkbox("Get a Triple S! (P4AU)", ref resultsTracks[7]);
                    DearImguiSharp.ImGui.Checkbox("Victory (P5D)", ref resultsTracks[8]);
                    DearImguiSharp.ImGui.Checkbox("After the Battle (P3D)", ref resultsTracks[9]);
                    DearImguiSharp.ImGui.Checkbox("Way to Go! (Persona Q)", ref resultsTracks[10]);
                    DearImguiSharp.ImGui.Checkbox("The Show is Over (PQ2)", ref resultsTracks[11]);
                }

                //var menuSize = new Vector2();
                //ImGui.__Internal.GetWindowSize((IntPtr)(&menuSize));

                //DearImguiSharp.ImGui.SetCursorPosY(menuSize.Y - 24);

                DearImguiSharp.ImGui.PushStyleColorVec4((int)ImGuiCol.Text, new ImVec4() { X = 128 / 255f, Y = 128 / 255f, Z = 128 / 255f, W = 221 / 255f });
                DearImguiSharp.ImGui.Text("v2.0.0 by YveltalGriffin");
                DearImguiSharp.ImGui.PopStyleColor(1);
            }
        }
        else DearImguiSharp.ImGui.End();
    }

    public void Suspend() => ImguiHook.Disable();
    public void Resume() => ImguiHook.Enable();
    public void Unload() => ImguiHook.Destroy();

    public static void Shuffle<T>(int n, T[] array)
    {
        Random rand = new Random();
        //int n = array.Length;

        while (n > 1)
        {
            int k = rand.Next(n--);
            T val = array[n];
            array[n] = array[k];
            array[k] = val;
        }

    }

    [Function(FunctionAttribute.Register.r10, FunctionAttribute.Register.rdx, false)]
    private delegate int ambushBGMDelegate();

    [Function(FunctionAttribute.Register.r10, FunctionAttribute.Register.rdx, false)]
    private delegate int battleBGMDelegate();

    [Function(FunctionAttribute.Register.r10, FunctionAttribute.Register.rdx, false)]
    private delegate int resultsBGMDelegate();

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }
    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}