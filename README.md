# Persona 5 Royal Battle Music Randomizer
<img src="preview.gif">

[Download the Reloaded II package on GameBanana!](https://gamebanana.com/mods/412327)

## Description:
Battle and victory theme randomizer, in the spirit of the RPCS3 random BGM patch for vanilla P5. The mod adds shuffled DLC tracks (and a few extra songs from P3D/P5D) to the Take Over, Last Surprise, and Triumph tracklists. Encounters with special battle music (minibosses, bosses) are unaffected.

*Steam only! Untested on Game Pass.*

*Requires [No Holdup Music mod](https://gamebanana.com/mods/408638) to work properly! Go install that first!*

**Note:** I haven't tested looping on all the custom tracks, but it should be working.

## Technical Details:
For the previous version of this mod I stuffed the extra tracks into new cues added to BGM.acb using ACE, which caused lots of weird glitches. This new version actually hijacks the DLC costume ACBs. The costume music flag is hardcoded to always be `01` and `BGM_01` has been replaced with a new ACB made from scratch in Cri Atom Craft. 

## Adding Tracks:
For now, adding or removing tracks requires using Atom Craft (or ACE, but it seems buggy) to recompile the ACB/AWB. If you do this, make sure the cues remain set to Shuffle, otherwise the randomization won't work. 

A future update will add some sort of menu so users can toggle individual tracks on and off.

## Installation:
Use the 1-click install, or download the archive manually and unzip it into your mods folder.

## Battle tracklist:
- Take Over (P5R)
- Last Surprise (P5)
- Mass Destruction  (P3)
- Time to Make History (P4G)
- A Lone Prayer (Persona 1)
- Normal Battle (Persona 2)
- Obelisk (Catherine)
- Reach Out to The Truth (P4D)
- The Ultimate (P4AU)
- The Whims of Fate (Yukihiro Fukutomi Remix)
- Last Surprise (Taku Takahashi Remix)
- Life Will Change (ATLUS Meguro Remix)
- Rivers In the Desert (Mito Remix)
## Victory tracklist:
- Triumph 
- After the Battle 
- Period 
- Dream of a Butterfly 
- Battle Results (Persona 2) 
- Old Enemy (SMT If...)
- Period (P4D Version) 
- The Ultimate (P4AU) 
- Victory/Triumph (P5D Version)
- After the Battle (P3D Version)

*Special thanks to Powercore2000 for sharing source code, offering advice, and helping track down the BGM ACB variables!*
