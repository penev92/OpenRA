#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class WithMoveAnimationInfo : ITraitInfo, Requires<WithSpriteBodyInfo>, Requires<IMoveInfo>
	{
		[Desc("Displayed while moving.")]
		[SequenceReference] public readonly string MoveSequence = "move";

		[Desc("Displayed while transitioning from standing to moving.")]
		public readonly string StandToMoveSequence = null;

		public object Create(ActorInitializer init) { return new WithMoveAnimation(init, this); }
	}

	public class WithMoveAnimation : ITick
	{
		readonly WithMoveAnimationInfo info;
		readonly IMove movement;
		readonly WithSpriteBody wsb;

		WPos cachedPosition;
		bool hasTransition;
		bool transitionPlaying;
		bool isMoving;

		public WithMoveAnimation(ActorInitializer init, WithMoveAnimationInfo info)
		{
			this.info = info;
			movement = init.Self.Trait<IMove>();
			wsb = init.Self.Trait<WithSpriteBody>();

			cachedPosition = init.Self.CenterPosition;
			hasTransition = !string.IsNullOrEmpty(info.StandToMoveSequence);
		}

		public void Tick(Actor self)
		{
			var oldCachedPosition = cachedPosition;
			cachedPosition = self.CenterPosition;

			var wasMoving = isMoving;

			// Flying units set IsMoving whenever they are airborne, which isn't enough for our purposes
			isMoving = movement.IsMoving && !self.IsDead && (oldCachedPosition - cachedPosition).HorizontalLengthSquared != 0;
			if (!isMoving && (wsb.DefaultAnimation.CurrentSequence.Name != wsb.Info.Sequence))
			{
				wsb.CancelCustomAnimation(self);
				return;
			}

			if (hasTransition && isMoving && !wasMoving)
			{
				transitionPlaying = true;
				wsb.PlayCustomAnimation(self, info.StandToMoveSequence,
				() =>
				{
					wsb.PlayCustomAnimationRepeating(self, info.MoveSequence);
					transitionPlaying = false;
				});

				return;
			}

			if (!transitionPlaying)
				wsb.DefaultAnimation.ReplaceAnim(isMoving ? info.MoveSequence : wsb.Info.Sequence);
		}
	}
}
