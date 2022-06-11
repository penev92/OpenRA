#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Pathfinder
{
	/// <summary>
	/// TODO: !!!
	/// </summary>
	sealed class LinePathGraph : DensePathGraph
	{
		readonly CPos[] cells;
		readonly CellInfo[] infos;

		public LinePathGraph(Locomotor locomotor, Actor actor, World world, BlockedByActor check,
			Func<CPos, int> customCost, Actor ignoreActor, bool laneBias, bool inReverse, CPos[] cells)
			: base(locomotor, actor, world, check, customCost, ignoreActor, laneBias, inReverse)
		{
			this.cells = cells;
			infos = new CellInfo[cells.Length];
		}

		protected override bool IsValidNeighbor(CPos neighbor)
		{
			// Enforce that we only search within the predefined list of cells.
			return cells.Any(c => c.Layer == neighbor.Layer && c.X == neighbor.X && c.Y == neighbor.Y);
		}

		int InfoIndex(CPos pos)
		{
			for (var i = 0; i < cells.Length; i++)
				if (cells[i].Layer == pos.Layer && cells[i].X == pos.X && cells[i].Y == pos.Y)
					return i;

			return -1;
		}

		public override CellInfo this[CPos pos]
		{
			get => infos[InfoIndex(pos)];
			set => infos[InfoIndex(pos)] = value;
		}
	}
}
