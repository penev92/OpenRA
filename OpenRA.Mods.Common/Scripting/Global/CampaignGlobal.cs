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

using Eluant;
using OpenRA.Scripting;

namespace OpenRA.Mods.Common.Scripting
{
	[ScriptGlobal("Campaign")]
	public class CampaignGlobal : ScriptGlobal
	{
		public CampaignGlobal(ScriptContext context)
			: base(context) { }

		[Desc("asdf")]
		[return: ScriptEmmyTypeOverride("T[]", "T")]
		public LuaTable ReadCampaignData(string campaignId)
		{
			var t = Context.CreateTable();
			return t;
		}

		[Desc("qwer")]
		[return: ScriptEmmyTypeOverride("T[]", "T")]
		public LuaTable ReadMissionData(string campaignId, string missionId)
		{
			var t = Context.CreateTable();
			return t;
		}

		[Desc("zxcv")]
		public void WriteCampaignData(string campaignId, [ScriptEmmyTypeOverride("T[]", "T")] LuaTable dataTable)
		{
		}

		[Desc("hyrt")]
		public void WriteMissionData(string campaignId, string missionId, [ScriptEmmyTypeOverride("T[]", "T")] LuaTable dataTable)
		{
		}

		// Read campaign data

		// Read mission data

		// Write campaign data

		// Write mission data

		// [BONUS] Write mission achievement (or have this as part of mission data?)
	}
}
