#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class ObserverPanelWidget : Widget
	{
		readonly World world;
		readonly WorldRenderer worldRenderer;

		[ObjectCreator.UseCtor]
		public ObserverPanelWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
		}

		protected ObserverPanelWidget(ObserverPanelWidget other)
			: base(other)
		{
			world = other.world;
			worldRenderer = other.worldRenderer;
		}

		public override void Draw()
		{

		}

		public override Widget Clone()
		{
			return new ObserverPanelWidget(this);
		}
	}
}
