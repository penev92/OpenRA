#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.MW.Traits
{
	[Desc("Renders barrels for units with the Turreted trait.")]
	public class WithMovingSpriteBarrelInfo : UpgradableTraitInfo, IRenderActorPreviewSpritesInfo, Requires<TurretedInfo>,
		Requires<ArmamentInfo>, Requires<RenderSpritesInfo>, Requires<BodyOrientationInfo>
	{
		[Desc("Sequence name to use.")]
		[SequenceReference] public readonly string Sequence = "barrel";

		[Desc("Displayed while moving.")]
		[SequenceReference] public readonly string MoveSequence = null;

		[Desc("Armament to use for recoil.")]
		public readonly string Armament = "primary";

		[Desc("Visual offset.")]
		public readonly WVec LocalOffset = WVec.Zero;

		public override object Create(ActorInitializer init) { return new WithMovingSpriteBarrel(init.Self, this); }

		public IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, RenderSpritesInfo rs, string image, int facings, PaletteReference p)
		{
			if (UpgradeMinEnabledLevel > 0)
				yield break;

			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			var armament = init.Actor.TraitInfos<ArmamentInfo>()
				.First(a => a.Name == Armament);
			var t = init.Actor.TraitInfos<TurretedInfo>()
				.First(tt => tt.Turret == armament.Turret);

			var anim = new Animation(init.World, image, () => t.InitialFacing);
			anim.Play(RenderSprites.NormalizeSequence(anim, init.GetDamageState(), Sequence));

			var turretOrientation = body.QuantizeOrientation(new WRot(WAngle.Zero, WAngle.Zero, WAngle.FromFacing(t.InitialFacing)), facings);
			var turretOffset = body.LocalToWorld(t.Offset.Rotate(turretOrientation));

			yield return new SpriteActorPreview(anim, turretOffset, turretOffset.Y + turretOffset.Z, p, rs.Scale);
		}
	}

	public class WithMovingSpriteBarrel : UpgradableTrait<WithMovingSpriteBarrelInfo>, ITick
	{
		public readonly Animation DefaultAnimation;
		readonly RenderSprites rs;
		readonly Actor self;
		readonly Armament armament;
		readonly Turreted turreted;
		readonly BodyOrientation body;
		readonly IMove movement;
		WPos cachedPosition;
		bool altMoveSeq;

		public WithMovingSpriteBarrel(Actor self, WithMovingSpriteBarrelInfo info)
			: base(info)
		{
			this.self = self;
			body = self.Trait<BodyOrientation>();
			armament = self.TraitsImplementing<Armament>()
				.First(a => a.Info.Name == Info.Armament);
			turreted = self.TraitsImplementing<Turreted>()
				.First(tt => tt.Name == armament.Info.Turret);

			rs = self.Trait<RenderSprites>();
			movement = self.Trait<IMove>();
			cachedPosition = self.CenterPosition;
			if (Info.MoveSequence == null || Info.MoveSequence == Info.Sequence)
				altMoveSeq = false;

			DefaultAnimation = new Animation(self.World, rs.GetImage(self), () => turreted.TurretFacing);
			DefaultAnimation.PlayRepeating(NormalizeSequence(self, Info.Sequence));
			rs.Add(new AnimationWithOffset(
				DefaultAnimation, () => BarrelOffset(), () => IsTraitDisabled, p => RenderUtils.ZOffsetFromCenter(self, p, 0)));

			// Restrict turret facings to match the sprite
			turreted.QuantizedFacings = DefaultAnimation.CurrentSequence.Facings;
		}

		public string NormalizeSequence(Actor self, string sequence)
		{
			return RenderSprites.NormalizeSequence(DefaultAnimation, self.GetDamageState(), sequence);
		}

		public virtual void Tick(Actor self)
		{
			if (!altMoveSeq)
				return;

			var oldCachedPosition = cachedPosition;
			cachedPosition = self.CenterPosition;

			// Flying units set IsMoving whenever they are airborne, which isn't enough for our purposes
			var isMoving = movement.IsMoving && !self.IsDead && (oldCachedPosition - cachedPosition).HorizontalLengthSquared != 0;
			if (isMoving ^ (DefaultAnimation.CurrentSequence.Name != Info.MoveSequence))
				return;

			DefaultAnimation.ReplaceAnim(isMoving ? Info.MoveSequence : Info.Sequence);
		}

		WVec BarrelOffset()
		{
			var localOffset = Info.LocalOffset + new WVec(-armament.Recoil, WDist.Zero, WDist.Zero);
			var turretOffset = turreted != null ? turreted.Position(self) : WVec.Zero;
			var turretOrientation = WRot.Zero;
			//var turretOrientation = turreted != null ? turreted.LocalOrientation(self) : WRot.Zero;

			var quantizedBody = body.QuantizeOrientation(self, self.Orientation);
			var quantizedTurret = body.QuantizeOrientation(self, turretOrientation);
			return turretOffset + body.LocalToWorld(localOffset.Rotate(quantizedTurret).Rotate(quantizedBody));
		}

		IEnumerable<WRot> BarrelRotation()
		{
			var b = self.Orientation;
			var qb = body.QuantizeOrientation(self, b);
			yield return WRot.FromYaw(b.Yaw - qb.Yaw);
			//yield return turreted.LocalOrientation(self) + WRot.FromYaw(b.Yaw - qb.Yaw);
			yield return qb;
		}
	}
}
