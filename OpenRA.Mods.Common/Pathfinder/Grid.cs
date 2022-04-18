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

namespace OpenRA.Mods.Common.Pathfinder
{
	/// <summary>
	/// Represents a simplistic grid of cells, where everything in the
	/// top-to-bottom and left-to-right range is within the grid.
	/// The grid conceptually exists on a single layer.
	/// </summary>
	/// <remarks>
	/// This means in <see cref="MapGridType.RectangularIsometric"/> some cells within a grid may lay off the map.
	/// Contrast this with <see cref="CellRegion"/> which maintains the simplistic grid in map space -
	/// ensuring the cells are therefore always within the map area.
	/// The advantage of Grid is that it has straight edges, making logic for adjacent grids easy.
	/// A CellRegion has jagged edges in RectangularIsometric, which makes that more difficult.
	/// </remarks>
	public struct Grid
	{
		/// <summary>
		/// Inclusive.
		/// </summary>
		public readonly CPos TopLeft;

		/// <summary>
		/// Exclusive.
		/// </summary>
		public readonly CPos BottomRight;

		public Grid(CPos topLeft, CPos bottomRight)
		{
			if (topLeft.Layer != bottomRight.Layer)
				throw new ArgumentException($"{nameof(topLeft)} and {nameof(bottomRight)} must have the same {nameof(CPos.Layer)}");
			TopLeft = topLeft;
			BottomRight = bottomRight;
		}

		public int Width => BottomRight.X - TopLeft.X;
		public int Height => BottomRight.Y - TopLeft.Y;

		/// <summary>
		/// Checks if the cell X and Y lie within the grid bounds. The cell layer must also match.
		/// </summary>
		public bool Contains(CPos cell)
		{
			return
				cell.X >= TopLeft.X && cell.X < BottomRight.X &&
				cell.Y >= TopLeft.Y && cell.Y < BottomRight.Y &&
				cell.Layer == TopLeft.Layer;
		}

		public override string ToString()
		{
			return $"{TopLeft}->{BottomRight}";
		}
	}
}
