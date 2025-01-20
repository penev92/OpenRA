### 1.
The shellmap `MainMenu` has 2 buttons:
 - `Campaign` - new, for campaign selection
 - `Missions` - the old MissionBrowser, untouched, for (re)playing an arbitrary mission. To ***later*** be enhanced with mission playthrough information:
    - when was this mission played
    - beaten difficuly, time, score
    - etc.

#
### 2.
- The `Campaign` button, via `MainMenuLogic` calls the shellmap's Lua script and tells it to open the `CampaignBrowser` screen. *[Where do the arguments (like fullscreen or not, images/videos to display, available list of campaigns) come from - `MainMenuLogic` or the Lua script?]*
- The shellmap calls ***\<something that has access to the FileSystem\>*** and asks it about all present campaigns *(official or user-made, packed or not; this is to be expanded on later, initially it can be a list in `mod.yaml`)*.
- The shellmap uses the new Lua API method to open the `CampaignBrowser` screen, giving it a list of available campaigns.
- The `CampaignBrowser` calls the shellmap's Lua script with the ID of the selected campaign
- The shellmap uses the new Lua API method to open the `MissionSelection` screen, providing a list of available missions, images/videos to display, callbacks, clickmaps, etc.
