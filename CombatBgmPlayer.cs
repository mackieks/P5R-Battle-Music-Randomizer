using System;
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

namespace BattleMusicRandomizer
{
    public class BGMRandomizer
    {
           
        //private IReloadedHooks _hooks;
        static bool[] ambushTracks = new bool[13];
        static bool[] battleTracks = new bool[13];
        static bool[] resultsTracks = new bool[13];
        
        static bool showWindow = true;
        static bool showAmbushConfig = true;
        static volatile int ambushCueID = 0;

        private IReverseWrapper<ShouldGiveBonusDelegate> _shouldGiveBonusReverseWrapper;

        private IAsmHook _shouldGiveBonusHook;

        public BGMRandomizer(IReloadedHooks hooks, ILogger logger, IModLoader modLoader)
        {
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
            modLoader.GetController<IStartupScanner>().TryGetTarget(out var startupScanner);

            if (startupScanner != null)
            {
                SetAmbushTheme(memory, logger, baseAddress,  startupScanner, hooks);
                // SetBattleTheme(memory, logger, baseAddress, startupScanner);
                // SetResultsTheme(memory, logger, baseAddress, startupScanner);
                StubBGMSwap(memory, logger, baseAddress, startupScanner);

            }
            else {

                logger.TextColor = logger.ColorRed;
                logger.Write("Set up scanner came back null!");
            }

           Start(modLoader, hooks, logger);
        }

        public async void Start(IModLoaderV1 loader, IReloadedHooks hooks, ILogger _logger)
        {

            loader.GetController<IReloadedHooks>().TryGetTarget(out hooks);

            SDK.Init(hooks);
            await ImguiHook.Create(RenderTestWindow, new ImguiHookOptions()
            {
                EnableViewports = false,
                IgnoreWindowUnactivate = false,
                Implementations = new List<IImguiHook>()
                {
                    new ImguiHookDx11()
                }
            }).ConfigureAwait(true); 
        }

        private unsafe void RenderTestWindow()
        {

            DearImguiSharp.ImGuiViewport main_viewport = DearImguiSharp.ImGui.GetMainViewport();
            DearImguiSharp.ImVec2 windowpos = new ImVec2() { X = main_viewport.WorkPos.X + 700, Y = main_viewport.WorkPos.Y + 20 };
            DearImguiSharp.ImGui.SetNextWindowSize(windowpos, 1 << 2);
            DearImguiSharp.ImGui.SetNextWindowBgAlpha(0.35f);

            var style = DearImguiSharp.ImGui.GetStyle();
            style.WindowRounding = 5.0f;
            var colors = style.Colors;
            colors[(int)DearImguiSharp.ImGuiCol.TitleBgActive] = new ImVec4() { X = 150 / 255f, Y = 0f, Z = 0f, W = 221 / 255f };
            colors[(int)DearImguiSharp.ImGuiCol.TitleBg] = new ImVec4() { X = 150 / 255f, Y = 0f, Z = 0f, W = 95 / 255f };
            colors[(int)DearImguiSharp.ImGuiCol.TitleBgCollapsed] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 95 / 255f };
            colors[(int)DearImguiSharp.ImGuiCol.MenuBarBg] = new ImVec4() { X = 250 / 255f, Y = 0f, Z = 0f, W = 100 / 255f };
            colors[(int)DearImguiSharp.ImGuiCol.ScrollbarGrab ] = new ImVec4() { X = 255 / 255f, Y = 0f, Z = 0f, W = 115 / 255f };
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

            style.Colors= colors;



            if (showWindow)
            {
                if (!DearImguiSharp.ImGui.Begin("Battle Music Randomizer Config ♫", ref showWindow, 0))
                {
                    DearImguiSharp.ImGui.End();
                } else
                {
                    DearImguiSharp.ImGui.Text("-Enable the songs you'd like to hear in each submenu\n-Press the Save button to apply your changes");
                    if(DearImguiSharp.ImGui.CollapsingHeaderTreeNodeFlags("Ambush Themes", 0)){
                        DearImguiSharp.ImGui.Checkbox("Track 0", ref ambushTracks[0]);
                        DearImguiSharp.ImGui.Checkbox("Track 1", ref ambushTracks[1]);
                        DearImguiSharp.ImGui.Checkbox("Track 2", ref ambushTracks[2]);
                        DearImguiSharp.ImGui.Checkbox("Track 3", ref ambushTracks[3]);
                        DearImguiSharp.ImGui.Checkbox("Track 4", ref ambushTracks[4]);
                        DearImguiSharp.ImGui.Checkbox("Track 5", ref ambushTracks[5]);
                        DearImguiSharp.ImGui.Checkbox("Track 6", ref ambushTracks[6]);
                        DearImguiSharp.ImGui.Checkbox("Track 7", ref ambushTracks[7]);
                        DearImguiSharp.ImGui.Checkbox("Track 8", ref ambushTracks[8]);
                        DearImguiSharp.ImGui.Checkbox("Track 9", ref ambushTracks[9]);
                        DearImguiSharp.ImGui.Checkbox("Track 10", ref ambushTracks[10]);
                        DearImguiSharp.ImGui.Checkbox("Track 11", ref ambushTracks[11]);
                    }
                    if (DearImguiSharp.ImGui.CollapsingHeaderTreeNodeFlags("Battle Themes", 0))
                    {
                        DearImguiSharp.ImGui.Checkbox("Track 0", ref battleTracks[0]);
                        DearImguiSharp.ImGui.Checkbox("Track 1", ref battleTracks[1]);
                        DearImguiSharp.ImGui.Checkbox("Track 2", ref battleTracks[2]);
                        DearImguiSharp.ImGui.Checkbox("Track 3", ref battleTracks[3]);
                        DearImguiSharp.ImGui.Checkbox("Track 4", ref battleTracks[4]);
                        DearImguiSharp.ImGui.Checkbox("Track 5", ref battleTracks[5]);
                    }
                    if (DearImguiSharp.ImGui.CollapsingHeaderTreeNodeFlags("Results Themes", 0))
                    {
                        DearImguiSharp.ImGui.Checkbox("Track 0", ref resultsTracks[0]);
                        DearImguiSharp.ImGui.Checkbox("Track 1", ref resultsTracks[1]);
                        DearImguiSharp.ImGui.Checkbox("Track 2", ref resultsTracks[2]);
                        DearImguiSharp.ImGui.Checkbox("Track 3", ref resultsTracks[3]);
                        DearImguiSharp.ImGui.Checkbox("Track 4", ref resultsTracks[4]);
                        DearImguiSharp.ImGui.Checkbox("Track 5", ref resultsTracks[5]);
                    }
                }
            }
        }

        public void Suspend() => ImguiHook.Disable();
        public void Resume() => ImguiHook.Enable();
        public void Unload() => ImguiHook.Destroy();


        public static void SetAmbushTheme(Memory memory, ILogger logger, long baseAddress, IStartupScanner startupScanner, IReloadedHooks hooks)
        {
            startupScanner.AddMainModuleScan("BA 8B 03 00 00 83 F8 01", delegate (PatternScanResult result)
            {
                long num = result.Offset + baseAddress;
                long num2 = result.Offset + baseAddress + 8;
                Int32 num3 = (Int32)num2;
                logger.Write($"Ambush theme address is {num - baseAddress}. Found = {result.Found}\n");
                if (result.Found)
                {

                    string[] function =
                    {
                        "use64",
                        "jnz endHook", // If they're already going to get the bonus we don't want to change that
                        $"push rax\npush rcx\npush r8\npush r10",
                        "sub rsp, 32",
                        $"{hooks.Utilities.GetAbsoluteCallMnemonics(ShouldGiveBonus, out _shouldGiveBonusReverseWrapper)}",
                        "add rsp, 32",
                        "cmp rax, 0",
                        "pop r10\npop r8\npop rcx\npop rax",
                        "label endHook"
                    };
                    _shouldGiveBonusHook = hooks.CreateAsmHook(function, result.Offset).Activate();


                }
                else
                {
                    logger.Write($"Oops! Couldn't find 'Take Over' BGM call!\n");
                }
            });

           

        }

        private bool ShouldGiveBonus()
        {
            return false;
        }

        private static void SetBattleTheme(Memory memory, ILogger logger, long baseAddress, IStartupScanner startupScanner)
        {
            startupScanner.AddMainModuleScan("BA 2C 01 00 00 49 8B CF E8", delegate (PatternScanResult result)
            {
                long num = result.Offset + baseAddress;
                logger.Write($"Battle theme address is {num - baseAddress}. Found = {result.Found}\n");
                if (result.Found)
                {
                    memory.SafeWrite(num + 1, (short)962, marshal: false);
                    logger.Write($"Wrote new cue ID to memory.\n");
                }
                else
                {
                    logger.Write($"Oops! Couldn't find 'Last Surprise' BGM call!\n");
                }
            });
        }

        private static void SetResultsTheme(Memory memory, ILogger logger, long baseAddress, IStartupScanner startupScanner)
        {
            startupScanner.AddMainModuleScan("BA 54 01 00 00 48 8B CE 00 00 33 c9", delegate (PatternScanResult result)
            {
                long num = result.Offset + baseAddress;
                logger.Write($"Results theme address is {num - baseAddress}. Found = {result.Found}\n");
                if (result.Found)
                {
                    memory.SafeWrite(num + 1, (short)962, marshal: false);
                    logger.Write($"Wrote new cue ID to memory.\n");
                }
                else
                {
                    logger.Write($"Oops! Couldn't find 'Triumph' BGM call!\n");
                }
            });
        }

        private static void StubBGMSwap(Memory memory, ILogger logger, long baseAddress, IStartupScanner startupScanner)
        {
            startupScanner.AddMainModuleScan("C7 08 83 FE 11 72 D9 EB 0C", delegate (PatternScanResult result)
            {
                long num = result.Offset + baseAddress;
                if (result.Found)
                {
                    logger.Write($"BGM ACB index pointer found. Address is {num - baseAddress}.\n");
                    memory.SafeWrite(num, 0xEBD97211FE8308C7, marshal: false);
                    memory.SafeWrite(num + 8, 0x0141B3A498BF4800, marshal: false);
                    memory.SafeWrite(num + 16, 0x9090909090000000, marshal: false);
                    logger.Write($"Wrote BGM ACB change stub to memory.\n");
                }
                else
                {
                    logger.Write($"Couldn't find BGM ACB index pointer!\n");
                }
            });   
        }

        

    }
}

