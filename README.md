# JEBlock (Jump/Emote Block)

 > [!IMPORTANT]
> - This is Vibe code, 100% created by AI (Claud and ChatGPT). No human has verified this code.
> - By using this, you agree that our metallic overloads may be harvesting your data.
> - If any genuine flesh'n'blood coder wants to provide an alternative, I'll happily pull this.

## What is JEBlock?
JEBlock is a Dalamud plugin, that prevents the user from jumping or using emotes when a user defined Loci-ID is present.

When applying an emote (through Puppeteer/Triggers), the target may break free by tapping jump or starting a different /emote. The accepted GagSpeak solution is to enable `HARDMODE PERMISSIONS` and forced emotes, however, this also grants access to ALL other emotes, and requires your partner to perform multiple steps to fully constrain and animation lock their subject.
 
## Installation
1. Open Dalamud's settings (type `/xlsettings`)
2. Click on the `[Experimental]` tab
3. Scroll down and find `Custom Plugin Repositories`, then copy and paste the following link
`https://raw.githubusercontent.com/feral-src/DalamudPlugins/main/repo.json`
6. Click `[Save]`
7. Open the plugin installer (type `/xlplugins`) then search for `JEBlock` and click `[Install]`
   
## Usage
1. Within `Loci` find the `ID:` number for the effect you wish to use (eg `69251d44-116a-4674-8520-527f9cba3be9`)
2. Type `/jeblock` to open up the plugins config, and paste the ID into the provided field
3. You are now set, whenever that `Loci` effect is present, your character should not be able to jump, or use emotes
 > [!TIP]
> This plugin also provides a `/jeblock endemote` command. This can be used in a GagSpeak trigger to reset your character to an idle stance once a restraint/restriction is set to `Disabled`



## Related links
- [GagSpeak by Cordelia](https://www.xivmodarchive.com/modid/96433)
- [Animations by Amborella](https://www.xivmodarchive.com/user/587548)
 > [!TIP]
  > Join Amborella's discord to access the **"Superpack"** containing all their animations in a single mod, aligned to `/wringhands`


