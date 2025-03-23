#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OpenRA.FileSystem;
using OpenRA.Mods.Common.Campaign;
using OpenRA.Mods.Common.Scripting;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Video;
using OpenRA.Widgets;
using TagLib.Ape;
using TagLib.Riff;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	class CampaignInfo
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
	}

	public class CampaignBrowserLogic : ChromeLogic
	{
		readonly ModData modData;
		readonly World world;
		readonly Action onStart;
		readonly Action onExit;
		readonly CampaignInfo[] availableCampaigns;
		readonly VideoPlayerWidget videoPlayer;
		readonly BackgroundWidget fullscreenVideoPlayer;
		readonly ScrollPanelWidget campaignList;
		readonly ScrollItemWidget campaignTemplate;

		string selectedCampaign;

		[ObjectCreator.UseCtor]
		public CampaignBrowserLogic(Widget widget, ModData modData, World world, Action onStart, Action onExit)
		{
			this.modData = modData;
			this.world = world;
			this.onStart = onStart;
			this.onExit = onExit;

			// TODO: Load these from anywhere. Initially from mod.yaml or missions.yaml,
			// later check the FileSystem for any campaign packages (official or user-made, packed or unpacked).
			availableCampaigns = new CampaignInfo[]
			{
				new CampaignInfo
				{
					Id = "atreides",
					Name = "House Atreides",
					Description = "Play as the noble House Atreides"
				},
				new CampaignInfo
				{
					Id = "ordos",
					Name = "House Ordos",
					Description = "Play as the incidious House Ordos"
				},
				new CampaignInfo
				{
					Id = "harkonnen",
					Name = "House Harkonnen",
					Description = "Play as the evil House Harkonnen"
				}
			};

			//videoPlayer = widget.Get<VideoPlayerWidget>("FACTION_SELECTION");

			//widget.Get<ButtonWidget>("ATREIDES").OnClick = () => CampaignSelected("atreides");
			//widget.Get<ButtonWidget>("ORDOS").OnClick = () => CampaignSelected("ordos");
			//widget.Get<ButtonWidget>("HARKONNEN").OnClick = () => CampaignSelected("harkonnen");

			var startButton = widget.Get<ButtonWidget>("START_BUTTON");
			startButton.IsDisabled = () => string.IsNullOrEmpty(selectedCampaign);
			startButton.OnClick = () => StartButtonClicked();

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => BackButtonClicked();

			//modData.Manifest.Missions
			var stringPool = new HashSet<string>(); // Reuse common strings in YAML
			var yaml = MiniYaml.Merge(modData.Manifest.Missions.Select(
				m => MiniYaml.FromStream(modData.DefaultFileSystem.Open(m), m, stringPool: stringPool)));

			var campaigns = yaml.ConvertAll(x => x.Key);



			PanelLoaded();

			campaignList = widget.Get<ScrollPanelWidget>("CAMPAIGN_LIST");
			campaignTemplate = widget.Get<ScrollItemWidget>("CAMPAIGN_TEMPLATE");
			PopulateCampaignList();
		}

		bool disposed;
		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
			}

			base.Dispose(disposing);
		}

		#region Events

		void PanelLoaded()
		{
			//PlayVideo(videoPlayer, "G_PLNT_E.VQA", onComplete: () => PlayVideo(videoPlayer, "G_PLN2_E.VQA"));
		}

		void CampaignSelected(string selected)
		{
			switch (selected)
			{
				case "atreides":
					selectedCampaign = "atreides";
					PlayVideo(videoPlayer, "A_MNTG_E.VQA");
					break;
				case "ordos":
					selectedCampaign = "ordos";
					PlayVideo(videoPlayer, "O_MNTG_E.VQA");
					break;
				case "harkonnen":
					selectedCampaign = "harkonnen";
					PlayVideo(videoPlayer, "H_MNTG_E.VQA");
					break;
			}
		}

		void StartButtonClicked()
		{
			//StopVideo(videoPlayer);
			//var campaignManager = modData.Manifest.Get<CampaignManager>();
			//campaignManager.StartNewCampaign(modData.Manifest.Id, selectedCampaign);


			//if (selectedCampaign != null && callback != null)
				//world.WorldActor.Trait<LuaScript>().InvokeCallback(callback, runtime, new string[1] { selectedCampaign });

			if (selectedCampaign != null)
				world.WorldActor.Trait<LuaScript>().StartCampaign(selectedCampaign);
		}

		void BackButtonClicked()
		{
			StopVideo(videoPlayer);

			Ui.CloseWindow();
			onExit();
		}

		#endregion

		#region Audio

		float cachedSoundVolume;
		float cachedMusicVolume;
		void MuteSounds()
		{
			cachedSoundVolume = Game.Sound.SoundVolume;
			cachedMusicVolume = Game.Sound.MusicVolume;
			Game.Sound.SoundVolume = Game.Sound.MusicVolume = 0;
		}

		void UnMuteSounds()
		{
			//if (cachedSoundVolume > 0)
			//	Game.Sound.SoundVolume = cachedSoundVolume;

			if (cachedMusicVolume > 0)
				Game.Sound.MusicVolume = cachedMusicVolume;
		}

		#endregion

		#region Video

		void PlayVideo(VideoPlayerWidget player, string video, Action onComplete = null)
		{
			if (modData.DefaultFileSystem.Exists(video))
			{
				StopVideo(player);

				player.LoadAndPlay(video);

				if (player.Video == null)
				{
					StopVideo(player);

					//ConfirmationDialogs.ButtonPrompt(modData,
					//	title: CantPlayTitle,
					//	text: CantPlayPrompt,
					//	onCancel: () => { },
					//	cancelText: CantPlayCancel);
				}
				else
				{
					// video playback runs asynchronously
					player.PlayThen(() =>
					{
						StopVideo(player);
						onComplete?.Invoke();
					});

					// Mute other distracting sounds
					MuteSounds();
				}
			}
		}

		void StopVideo(VideoPlayerWidget player)
		{
			UnMuteSounds();
			player?.Stop();
		}

		#endregion

		void PopulateCampaignList()
		{
			campaignList.RemoveChildren();

			foreach (var campaign in availableCampaigns)
			{
				var item = ScrollItemWidget.Setup(campaignTemplate,
					() => selectedCampaign == campaign.Id,
					() => selectedCampaign = campaign.Id);

				var label = item.Get<LabelWithTooltipWidget>("TITLE");
				WidgetUtils.TruncateLabelToTooltip(label, campaign.Name);
				label.GetTooltipText = () => campaign.Description;

				campaignList.AddChild(item);
			}
		}

		//void StartCampaignClicked(Action onExit)
		//{
		//	// If selected mission becomes unavailable, exit MissionBrowser to refresh
		//	var map = modData.MapCache.GetUpdatedMap(selectedMap.Uid);
		//	if (map == null)
		//	{
		//		Game.Disconnect();
		//		Ui.CloseWindow();
		//		onExit();
		//		return;
		//	}

		//	selectedMap = modData.MapCache[map];
		//	var orders = new List<Order>();

		//	foreach (var option in missionOptions)
		//		orders.Add(Order.Command($"option {option.Key} {option.Value}"));

		//	orders.Add(Order.Command($"state {Session.ClientState.Ready}"));

		//	var missionData = selectedMap.WorldActorInfo.TraitInfoOrDefault<MissionDataInfo>();
		//	if (missionData != null && missionData.StartVideo != null && modData.DefaultFileSystem.Exists(missionData.StartVideo))
		//	{
		//		var fsPlayer = fullscreenVideoPlayer.Get<VideoPlayerWidget>("PLAYER");
		//		fullscreenVideoPlayer.Visible = true;
		//		PlayVideo(fsPlayer, missionData.StartVideo, PlayingVideo.GameStart,
		//			() => Game.CreateAndStartLocalServer(selectedMap.Uid, orders));
		//	}
		//	else
		//		Game.CreateAndStartLocalServer(selectedMap.Uid, orders);
		//}
	}
}
