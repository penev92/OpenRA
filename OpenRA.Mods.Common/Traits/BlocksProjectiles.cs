#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor blocks bullets and missiles with 'Blockable' property.")]
	public class BlocksProjectilesInfo : ConditionalTraitInfo, IBlocksProjectilesInfo
	{
		public readonly WDist Height = WDist.FromCells(1);

		[Desc("Blocks projectiles from actors of players with these stances.")]
		public readonly Stance Stances = Stance.Ally | Stance.Neutral | Stance.Enemy;

		public override object Create(ActorInitializer init) { return new BlocksProjectiles(init.Self, this); }
	}

	public class BlocksProjectiles : ConditionalTrait<BlocksProjectilesInfo>, IBlocksProjectiles
	{
		public BlocksProjectiles(Actor self, BlocksProjectilesInfo info)
			: base(info) { }

		WDist IBlocksProjectiles.BlockingHeight { get { return Info.Height; } }

		public Stance BlockingStances { get { return Info.Stances; } }
		
		public static bool AnyBlockingActorsBetween(World world, Actor sourceActor, WPos start, WPos end, WDist width, WDist overscan, out WPos hit)
		{
			var actors = world.FindActorsOnLine(start, end, width, overscan);
			var length = (end - start).Length;

			foreach (var a in actors)
			{
				var stance = sourceActor.Owner.Stances[a.Owner];

				var blockers = a.TraitsImplementing<IBlocksProjectiles>()
					.Where(Exts.IsTraitEnabled)
					.Where(b => b.BlockingStances.HasStance(stance))
					.ToList();

				if (!blockers.Any())
					continue;

				var hitPos = WorldExtensions.MinimumPointLineProjection(start, end, a.CenterPosition);
				var dat = world.Map.DistanceAboveTerrain(hitPos);
				if ((hitPos - start).Length < length && blockers.Any(t => t.BlockingHeight > dat))
				{
					hit = hitPos;
					return true;
				}
			}

			hit = WPos.Zero;
			return false;
		}
	}
}
