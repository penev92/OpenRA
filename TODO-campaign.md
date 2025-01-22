### 1.
The shellmap `MainMenu` has 2 buttons:
 - `Campaign` - new, for campaign selection
 - `Missions` - the old MissionBrowser, untouched, for (re)playing an arbitrary mission. To ***later*** be enhanced with mission playthrough information:
    - when was this mission played
    - beaten difficuly, time, score
    - etc.

#
### 2.
- The `Campaign` button, via `MainMenuLogic`, opens the `CampaignBrowser` screen.
- The `CampaignBrowser` screen shows a list of available campaigns *(official or user-made, packed or not; this is to be expanded on later, initially it can be a list in `mod.yaml` or `missions.yaml`)*.
- The `CampaignBrowser` calls the shellmap's Lua script with the ID of the selected campaign.
- (IF APPLICABLE) The shellmap uses the Lua API to play any opening FMV(s).
- (IF APPLICABLE) The shellmap uses the **new** Lua API method to open the `MissionSelection` screen, providing a list of available missions, images/videos to display, callbacks, clickmaps, etc.
- The shellmap uses the **new** Lua API method to switch to the first mission in the campaign (or the one selected by the user if there is a selection screen).

This is the simple, TS-like approach where all campaigns are listed as names - https://steamuserimages-a.akamaihd.net/ugc/5845183706736717436/0ECE4DAF9401C694E2936287B71354E40F9BFB89/. Later we should add a separation of "default" campaigns vs "extra" campaigns. Then the "default" ones can get a fancy selection screen like in original TD or D2k, with a secondary screen to still list the "extra" campaigns like the initial implementation - https://cdn.discordapp.com/attachments/548627097099173895/1331379442634985513/image.png?ex=6791673a&is=679015ba&hm=972c150cf33bdae063aaf5e52360c7cfa038a89df4a50b962a89d07096f4f4de&
