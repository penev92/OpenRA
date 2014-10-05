#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Traits;
using OpenRA.Scripting;
using OpenRA.Mods.Common.Traits.Air;
using OpenRA.Mods.Common.Activities.Air;

namespace OpenRA.Mods.RA.Scripting
{
	[ScriptPropertyGroup("Movement")]
	public class PlaneProperties : ScriptActorProperties, Requires<PlaneInfo>
	{
		public PlaneProperties(ScriptContext context, Actor self)
			: base(context, self) { }

		[ScriptActorPropertyActivity]
		[Desc("Fly within the cell grid.")]
		public void Move(CPos cell)
		{
			self.QueueActivity(new Fly(self, Target.FromCell(self.World, cell)));
		}
	}
}