#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class SpriteHarvesterDockSequence : HarvesterDockSequence
	{
		readonly WithSpriteBody wsb;
		readonly WithDockingAnimation wda;

		public SpriteHarvesterDockSequence(Actor self, Actor refinery, int dockAngle, bool isDragRequired, WVec dragOffset, int dragLength)
			: base(self, refinery, dockAngle, isDragRequired, dragOffset, dragLength)
		{
			wsb = self.Trait<WithSpriteBody>();
			wda = self.Trait<WithDockingAnimation>();
		}

		public override Activity OnStateDock(Actor self)
		{
			foreach (var trait in self.TraitsImplementing<INotifyHarvesterAction>())
				trait.Docked();

			wsb.PlayCustomAnimation(self, wda.Info.DockSequence, () => wsb.PlayCustomAnimationRepeating(self, wda.Info.DockLoopSequence));
			dockingState = State.Loop;
			return this;
		}

		public override Activity OnStateUndock(Actor self)
		{
			foreach (var trait in self.TraitsImplementing<INotifyHarvesterAction>())
				trait.Undocked();

			wsb.PlayCustomAnimationBackwards(self, wda.Info.DockSequence, () => dockingState = State.Complete);
			dockingState = State.Wait;
			return this;
		}
	}
}