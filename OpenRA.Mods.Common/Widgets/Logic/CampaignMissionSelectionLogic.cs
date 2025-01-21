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
	public class CampaignMissionSelectionLogic : ChromeLogic
	{
		readonly ModData modData;
		readonly World world;
		readonly Action onStart;
		readonly Action onExit;
		readonly string[] availableMissions;
		readonly VideoPlayerWidget videoPlayer;
		readonly BackgroundWidget fullscreenVideoPlayer;
		readonly ScrollPanelWidget missionList;
		readonly ScrollItemWidget missionTemplate;

		string selectedMission;

		[ObjectCreator.UseCtor]
		public CampaignMissionSelectionLogic(Widget widget, ModData modData, World world, Action onStart, Action onExit, string[] availableMissions)
		{
			this.modData = modData;
			this.world = world;
			this.onStart = onStart;
			this.onExit = onExit;

			var startButton = widget.Get<ButtonWidget>("START_BUTTON");
			startButton.IsDisabled = () => string.IsNullOrEmpty(selectedMission);
			startButton.OnClick = () => StartButtonClicked();

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => BackButtonClicked();

			// Hide the video player for the time being...
			widget.Get<ContainerWidget>("MISSION_INFO").IsVisible = () => false;
			widget.Get<BackgroundWidget>("MISSION_BIN").IsVisible = () => false;

			PanelLoaded();

			missionList = widget.Get<ScrollPanelWidget>("MISSION_LIST");
			missionTemplate = widget.Get<ScrollItemWidget>("MISSION_TEMPLATE");
			PopulateMissionList();
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

		void MissionSelected(string selected)
		{
		}

		void StartButtonClicked()
		{
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
			player.Stop();
		}

		#endregion

		void PopulateMissionList()
		{
			missionList.RemoveChildren();

			foreach (var mission in availableMissions)
			{
				var item = ScrollItemWidget.Setup(missionTemplate,
					() => selectedMission == mission,
					() => selectedMission = mission);

				var label = item.Get<LabelWithTooltipWidget>("TITLE");
				WidgetUtils.TruncateLabelToTooltip(label, mission);
				label.GetTooltipText = () => mission;

				missionList.AddChild(item);
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
