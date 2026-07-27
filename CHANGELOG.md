## \## 1.0.0/1.0.1

## \- Initial release

## \## 1.0.2

## \- Improved README

## \## 1.0.3

## Balance:

## \- Reduced Vecna's spawn weight from 64 to 30 to prevent him from appearing too frequently.

## Bug Fixes:

## \- Added a safety check to clocks so they don't spawn inside of walls

## \- Added a safety check to ensure Vecna doesn't kill players outside of chase.

## \- Added a potential fix to cosmetics clipping into first person view after chase.

## \- Added a safety override to make sure Vecna's visibility game layer wasn't overwritten by other mods to prevent him being invisible in chase.

## Misc

## \- Fixed grammar and spelling mistakes in README

## 

## \## 1.0.4

## \- Updated mod to be compatible with v81

## Bug Fixes:

## \- Fixed a bug that caused the company cruiser to sink into the floor when vecna spawned. More features and QoL improvements to come in future updates around the cruiser!

## \- Thank you to Starpinguin25 for reporting this issue to me!

## \## 1.1.0

## Features:

## \- Added spawn weight per moon that is customisable in config

## \- Added new functionality around the company cruiser involving clocks and the chase

## \- Added a new cinematic when cursed player is in company cruiser when Vecna tries to trigger chase

## \- Added new voiceline for new cinematic

## \- Fixed an issue where others could see items in the new utility item slot, introduced in v81, when in chase

## Balance:

## \- Vecna now has a lower chance to spawn on easier moons, with a much higher chance to spawn on more diffcult moons. This is to give players an opportunity to buy a boombox to counteract Vecna when he is more common.

## \## 1.1.1

## Bug Fixes:

## \- Fixed a bug that made enemy meshes disappear to victim if they escaped chase

## \- Fixed a bug that resulted in player voice chat not being muted in chase

## \- Continuing to monitor issues caused by v81. Please report any bugs you find!

## \## 1.1.2

## \- Fixed a bug where you couldn't hear other players in spectate mode after dying to Vecna.

## 

## \# 2.0.0

## \## NEW GAMEPLAY MECHANICS

## \### Vecna's Lair

## \- Added a new pocket dimension that contains Vecna's Lair (creel house attic)

## \- Added a new upside down portal system to traverse to Vecna's Lair. The entrance portal spawns at a random position in the interior. (thanks to jon4rez for portal effects)

## \- Added an ability to break Vecna's curse permanently by disrupting Vecna in his lair (clocks no longer spawn)

## \### Vecna's Wrath

## \- Added a new behavioural phase, once Vecna has been disturbed, where he becomes visible and is a threat to everyone.

## \- Added a new chase mode in 'hunt everyone' where Vecna will know where everybody is at ALL times, gaining the ability to traverse outside

## \- Added new 'hunt everyone' ability that allows Vecna to telekinetically push and damage players inside of facility

## \- Added new 'hunt everyone' ability that allows Vecna to telekinetically throw players when outside of the facility

## \- Added a new 'hunt everyone' ability that allows Vecna to telekinetically blast a player(s) away from him dealing damage

## \### A chance to fight back

## \- In 'hunt everyone' mode ONLY, Vecna is able to be stunned by players

## \- In 'hunt everyone' mode ONLY, Vecna is able to be killed by players

## \- In 'hunt everyone' mode ONLY, Vecna is able to break into the ship

## 

## \## GAMEPLAY IMPROVEMENTS

## \- Improved logic of hiding player renderers to be less impactful on performance

## \- Improved light interference during clock haunts to use the base games method, which is more performant and less resource intensive.

## \- Added compatibility support with ModelReplacementAPI and MoreCompany so cosmetics are now fully hidden when a player is taken into a trance.

## \- Added compatibility with MelaniesVoice, meaning TTS is muted when player is in trance

## \- Added voice muting compatibility with MoreCompany so players are correctly muted in trance

## \- Changed the behaviour of Vecna's clock slightly. It now only disappears when the player is not looking at it, to avoid jarring clock disappearances that break immersion.

## \- Now VR Compatible.

## \## AUDIO IMPROVEMENTS

## \- Improved the clock haunting chimes to progressively become more aggressive as Vecna's curse grows stronger

## \- Improved Vecna's breathing audio to not phase in and out

## \- Added new footsteps audio for Vecna

## \- Added new Vecna voice lines for when he is stunned by a player and dies.

## \- Added new telekinesis sound effects for when Vecna uses his abilities

## \- Added new portal sound effects for portal usage

## 

## \## VISUAL IMPROVEMENTS

## \- Completely reworked the appearance of Vecna's clock to look more realistic and similar to the showw

## \- Added a new aura effect to Vecna (thanks to jon4rez)

## \- Added a telekinesis distortion effect when Vecna uses his abilities (thanks to jon4rez)

## \- Improved the clone limb snap animation

## \- Added better blood effects to the limb snap

## 

## \## ANIMATION IMPROVEMENTS

## \- Added new walking animation for Vecna

## \- Added a new execution animation for Vecna

## \- Added telekinesis casting animations for Vecna

## \- Added an animation when Vecna exit's his lair

## \- Added an animation where Vecna meditates in his lair

## \- Added an animation where Vecna detaches from his Tentacles in his lair

## \- Added upside down tentacles that animation with Vecna when he meditates and detaches

## \- Added new player animation for when they are pulled in by Vecna

## \- Added new stunned and death animation for Vecna

## \- Added a new animation for Vecna's blast ability

## 

## \## BUG FIXES

## \- Fixed a bug where not all renderers would be hidden

## \- Fixed a bug where victim voices would not be muted

## \- Fixed a bug where Vecna would sometimes not kill players

## 

## \## REMOVED

## \- Removed the cruiser cutscene for functionality reasons, and to reduce code bloat. Instead, the clock will just spawn on bonnet and chase will start upon vehicle exit

## \- Removed VFX when Clocks disappear to accommodate new clock logic

