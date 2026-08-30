# JEBlock (Jump/Emote Block)

## Important Information & Disclosure
- This is Vibe code, 100% created by AI (Claud and ChatGPT). No human has verified this code
- By using this, you agree that our metallic overloads may be harvesting your data
- If any genuine flesh'n'blood coder wants to provide an alternative, I'll happily pull this.

## What is JEBlock?
JEBlock is a Dalamud plugin, that prevents the user from jumping or using emotes when a trigger /emote is being performed.

This plugin has been created to compliment
- [GagSpeak by Cordelia](https://www.xivmodarchive.com/modid/96433)
- [Animations by Aamborella](https://www.xivmodarchive.com/user/587548)
  
## Why was it created?
When using GagSpeak with Puppeteer/Triggers to apply an emote, the target can break free by tapping jump or starting a different /emote. The accepted solution is to enable `HARDMODE PERMISSIONS` and grant access to `Force emotes`, however this unfortunately grants access to ALL emotes, and requires your partner to perform multiple steps to fully constrain and animation lock their subject.

## Alternative solution
- Assign your preferred Amborella emote to a restraint/restriction, ensure it has "Block all movement" selected.
- Set-up a Trigger/Puppetteer to perform the `/wringhands` emote for the above.
- Install JEBlock (no configuration is needed).
  
Now, when that restraint/restriction has been enabled, `/wringhands` will be triggered, and detected. Jumping and performing other emotes will be blocked. 

Once the GagSpeak restriction/restraint is removed, the player will be able to move (which cancels the animation), and sets the plugin back to an inactive state.

# Installation
1. Open Dalamud's settings (type `/xlsettings`)
2. Click on the `[Experimental]` tab
3. Scroll down and find `Custom Plugin Repositories`, then copy and paste the following link
4. `https://raw.githubusercontent.com/feral-src/repo.json`
5. Click `[Save]`
6. Open the plugin installer (type `/xlplugins`) then search for `JEBlock` and click `[Install]`
   





