using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sigscan.Definitions.Structs;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using System.Numerics;

namespace BattleMusicRandomizer
{
    public class BGMRandomizer
    {
        public BGMRandomizer(IReloadedHooks hooks, ILogger logger, IModLoader modLoader)
        {
            
            Memory memory = Memory.Instance;
            using Process thisProcess = Process.GetCurrentProcess();
            long baseAddress = thisProcess.MainModule!.BaseAddress.ToInt64();
            modLoader.GetController<IStartupScanner>().TryGetTarget(out var startupScanner);

            if (startupScanner != null)
            {
                // SetAmbushTheme(memory, logger, baseAddress,  startupScanner);
                // SetBattleTheme(memory, logger, baseAddress, startupScanner);
                // SetResultsTheme(memory, logger, baseAddress, startupScanner);
                StubBGMSwap(memory, logger, baseAddress, startupScanner);
            }
            else {

                logger.TextColor = logger.ColorRed;
                logger.Write("Set up scanner came back null!");
            }

        }

        private static void SetAmbushTheme(Memory memory, ILogger logger, long baseAddress, IStartupScanner startupScanner)
        {

            startupScanner.AddMainModuleScan("BA 8B 03 00 00 83 F8 01", delegate (PatternScanResult result)
            {
                long num = result.Offset + baseAddress;
                logger.Write($"Ambush theme address is {num - baseAddress}. Found = {result.Found}\n");
                if (result.Found)
                {  
                    memory.SafeWrite(num + 1, (short)962, marshal: false);
                    logger.Write($"Wrote new cue ID to memory.\n");
                }
                else
                {
                    logger.Write($"Oops! Couldn't find 'Take Over' BGM call!\n");
                }
            });
        }

        private static void SetBattleTheme(Memory memory, ILogger logger, long baseAddress, IStartupScanner startupScanner)
        {
            startupScanner.AddMainModuleScan("BA 2C 01 00 00 49 8B CF", delegate (PatternScanResult result)
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
            startupScanner.AddMainModuleScan("C7 08 83 FE 11 72 D9 EB 0C 48 63 C6 49 8D 3C C6", delegate (PatternScanResult result)
            {
                long num = result.Offset + baseAddress;
                if (result.Found)
                {
                    logger.Write($"BGM ACB index pointer found. Address is {num - baseAddress}.\n");
                    memory.SafeWrite(num, 0xEBD97211FE8308C7, marshal: false);
                    memory.SafeWrite(num + 8, 0x0141ADC0F8BF4800, marshal: false);
                    memory.SafeWrite(num + 16, 0X9090909090000000, marshal: false);
                    memory.SafeWrite(num + 24, 0xAC360D8B4807B70F, marshal: false);
                    memory.SafeWrite(num + 32, 0xED37B538158BED37, marshal: false);
                    memory.SafeWrite(num + 40, 0x010D840FD0390189, marshal: false);
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

