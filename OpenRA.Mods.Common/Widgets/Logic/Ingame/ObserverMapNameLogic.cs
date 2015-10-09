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
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class ObserverMapNameLogic
	{
		[ObjectCreator.UseCtor]
		public ObserverMapNameLogic(Widget widget, World world)
		{
			var label = widget as LabelWidget;
			label.Text = world.Map.Title;
		}
	}
}
