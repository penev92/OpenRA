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

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Commands;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Renders a visualization overlay showing how the pathfinder searches for paths. Attach this to the world actor.")]
	public class PathfindingOverlayInfo : TraitInfo, Requires<PathFinderInfo>
	{
		public readonly Color TargetLineColor = Color.Red;
		public readonly Color AbstractNodeColor = Color.Blue;
		public readonly Color CellColor = Color.Yellow;

		public override object Create(ActorInitializer init) { return new PathfindingOverlay(this); }
	}

	public class PathfindingOverlay : ITick, IRenderAnnotations, IWorldLoaded, IChatCommand
	{
		const string CommandName = "hpf-show";
		const string CommandDesc = "toggles a visualization of path searching.";

		readonly PathfindingOverlayInfo info;
		int ticksLeftToShow;

		public bool IsAbstractSearch;
		public bool Enabled { get; private set; }

		CPos sourceCell;
		CPos targetCell;
		public List<CPos> Cells = new List<CPos>();
		public List<GraphEdge> Edges = new List<GraphEdge>();
		public List<CPos> AbstractNodes = new List<CPos>();
		public List<GraphEdge> AbstractEdges = new List<GraphEdge>();

		public PathfindingOverlay(PathfindingOverlayInfo info)
		{
			this.info = info;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var console = w.WorldActor.TraitOrDefault<ChatCommands>();
			var help = w.WorldActor.TraitOrDefault<HelpCommand>();

			if (console == null || help == null)
				return;

			console.RegisterCommand(CommandName, this);
			help.RegisterHelp(CommandName, CommandDesc);
		}

		void IChatCommand.InvokeCommand(string name, string arg)
		{
			if (name == CommandName)
				Enabled ^= true;
		}

		public void Start(CPos source, CPos target)
		{
			sourceCell = targetCell = CPos.Zero;
			Cells.Clear();
			Edges.Clear();
			AbstractNodes.Clear();
			AbstractEdges.Clear();

			ticksLeftToShow = 600;
			IsAbstractSearch = true;
			sourceCell = source;
			targetCell = target;
		}

		public void Tick(Actor self)
		{
			if (ticksLeftToShow > 0)
				ticksLeftToShow--;

			if (ticksLeftToShow == 0)
			{
				sourceCell = targetCell = CPos.Zero;
				Cells.Clear();
				Edges.Clear();
				AbstractNodes.Clear();
				AbstractEdges.Clear();
			}
		}

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (!Enabled || ticksLeftToShow <= 0)
				yield break;

			// Target line:
			yield return new TargetLineRenderable(new[]
			{
				self.World.Map.CenterOfSubCell(sourceCell, SubCell.FullCell),
				self.World.Map.CenterOfSubCell(targetCell, SubCell.FullCell),
			}, info.TargetLineColor, 10, 10);

			// Abstract graph:
			foreach (var node in AbstractNodes)
			{
				yield return new TargetLineRenderable(new[]
					{
						self.World.Map.CenterOfSubCell(node, SubCell.FullCell),
						self.World.Map.CenterOfSubCell(node, SubCell.FullCell)
					},
					info.AbstractNodeColor, 10, 10);
			}

			foreach (var edge in AbstractEdges)
			{
				yield return new TargetLineRenderable(new[]
				{
					self.World.Map.CenterOfSubCell(edge.Source, SubCell.FullCell),
					self.World.Map.CenterOfSubCell(edge.Destination, SubCell.FullCell),
				}, info.AbstractNodeColor, 6, 6);
			}

			// Full graph:
			foreach (var cell in Cells)
			{
				yield return new TargetLineRenderable(new[]
					{
						self.World.Map.CenterOfSubCell(cell, SubCell.FullCell),
						self.World.Map.CenterOfSubCell(cell, SubCell.FullCell)
					},
					info.CellColor, 5, 5);
			}

			foreach (var edge in Edges)
			{
				yield return new TargetLineRenderable(new[]
				{
					self.World.Map.CenterOfSubCell(edge.Source, SubCell.FullCell),
					self.World.Map.CenterOfSubCell(edge.Destination, SubCell.FullCell),
				}, info.CellColor, 2, 2);
			}
		}

		bool IRenderAnnotations.SpatiallyPartitionable => false;
	}
}
