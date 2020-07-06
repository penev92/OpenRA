#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class Fly : Activity
	{
		readonly Aircraft aircraft;
		readonly WDist maxRange;
		readonly WDist minRange;
		readonly Color? targetLineColor;
		readonly WDist nearEnough;
		readonly int finalSpeed;
		readonly WAngle? finalFacing = null;

		Target target;
		Target lastVisibleTarget;
		bool useLastVisibleTarget;
		readonly List<WPos> positionBuffer = new List<WPos>();

		public Fly(Actor self, Target t, WDist nearEnough, WPos? initialTargetPosition = null, Color? targetLineColor = null)
			: this(self, t, initialTargetPosition, targetLineColor)
		{
			this.nearEnough = nearEnough;
		}

		public Fly(Actor self, Target t, WPos? initialTargetPosition = null, Color? targetLineColor = null, int speed = -1,
			WAngle? facing = null)
		{
			aircraft = self.Trait<Aircraft>();
			target = t;
			this.targetLineColor = targetLineColor;
			finalSpeed = speed >= 0 ? speed : aircraft.IdleSpeed;
			finalFacing = facing;

			// The target may become hidden between the initial order request and the first tick (e.g. if queued)
			// Moving to any position (even if quite stale) is still better than immediately giving up
			if ((target.Type == TargetType.Actor && target.Actor.CanBeViewedByPlayer(self.Owner))
			    || target.Type == TargetType.FrozenActor || target.Type == TargetType.Terrain)
				lastVisibleTarget = Target.FromPos(target.CenterPosition);
			else if (initialTargetPosition.HasValue)
				lastVisibleTarget = Target.FromPos(initialTargetPosition.Value);
		}

		public Fly(Actor self, Target t, WDist minRange, WDist maxRange,
			WPos? initialTargetPosition = null, Color? targetLineColor = null)
			: this(self, t, initialTargetPosition, targetLineColor)
		{
			this.maxRange = maxRange;
			this.minRange = minRange;
		}

		public static void FlyTick(Actor self, Aircraft aircraft, WAngle? desiredPitch = null, WAngle? desiredFacing = null,
			WAngle? desiredBodyFacing = null, int desiredSpeed = -1, WAngle? desiredTurnSpeed = null)
		{
			// Acceleration
			desiredSpeed = desiredSpeed >= 0 ? desiredSpeed : aircraft.MovementSpeed;
			var speed = Util.TickSpeed(aircraft.CurrentSpeed, desiredSpeed, aircraft.Acceleration);

			// Prevent oversteering by predicting our angular deceleration curve.
			var desiredFlightTurnSpeed = desiredTurnSpeed ?? WAngle.Zero;
			if (desiredFacing.HasValue)
			{
				var flightFacingDelta = Math.Abs((desiredFacing.Value - aircraft.FlightFacing).Angle2);
				if (flightFacingDelta >= aircraft.CurrentFlightTurnSpeed.Angle2 * aircraft.CurrentFlightTurnSpeed.Angle2 / aircraft.TurnAcceleration.Angle2 / 2)
					desiredFlightTurnSpeed = (desiredFacing.Value - aircraft.FlightFacing).Clamp(-aircraft.TurnSpeed, aircraft.TurnSpeed);
			}

			// Angular acceleration
			var flightTurnSpeed = Util.TickFacing(aircraft.CurrentFlightTurnSpeed, desiredFlightTurnSpeed, aircraft.TurnAcceleration);
			var flightFacing = aircraft.FlightFacing + flightTurnSpeed;

			// Try to stay at cruising altitude unless instructed otherwise.
			var desiredFlightPitch = aircraft.FlightPitch;
			if (desiredPitch == null)
				desiredFlightPitch = new WAngle(aircraft.InclineLookahead()).Clamp(-aircraft.Info.MaximumPitch, aircraft.Info.MaximumPitch);
			else
				desiredFlightPitch = desiredPitch.Value;

			var flightPitch = Util.TickFacing(aircraft.FlightPitch, desiredFlightPitch, aircraft.Info.PitchSpeed);

			// If we can slide, independently turn the aircraft body.
			var bodyTurnSpeed = WAngle.Zero;
			var bodyFacing = flightFacing;
			if (aircraft.Info.CanSlide)
			{
				var bodyFacingDelta = (desiredBodyFacing ?? desiredFacing ?? aircraft.FlightFacing) - aircraft.Facing;
				var desiredBodyTurnSpeed = WAngle.Zero;
				if (Math.Abs(bodyFacingDelta.Angle2) >= aircraft.CurrentBodyTurnSpeed.Angle2 * aircraft.CurrentBodyTurnSpeed.Angle2 / aircraft.BodyTurnAcceleration.Angle2 / 2)
					desiredBodyTurnSpeed = bodyFacingDelta.Clamp(-aircraft.BodyTurnSpeed, aircraft.BodyTurnSpeed);

				bodyTurnSpeed = Util.TickFacing(aircraft.CurrentBodyTurnSpeed, desiredBodyTurnSpeed, aircraft.BodyTurnAcceleration);
				bodyFacing = aircraft.Facing + bodyTurnSpeed;
			}

			// Determine body roll and pitch offsets due to banking while turning.
			var roll = WAngle.Zero;
			var pitch = WAngle.Zero;
			if (aircraft.Info.Roll != WAngle.Zero)
			{
				long cent = aircraft.Info.Roll.Angle2 * speed * flightTurnSpeed.Angle2;
				roll = new WAngle((int)(-cent * (flightFacing - bodyFacing).Cos() / (1024 * aircraft.TurnSpeed.Angle * aircraft.Info.Speed)));
				pitch = new WAngle((int)(cent * (flightFacing - bodyFacing).Sin() / (1024 * aircraft.TurnSpeed.Angle * aircraft.Info.Speed)));
			}

			// Determine body roll and pitch offsets depending on horizontal forward speed.
			if (aircraft.Info.Pitch != WAngle.Zero)
			{
				long forw = aircraft.Info.Pitch.Angle2 * speed * flightPitch.Cos();
				pitch += new WAngle((int)(forw * (flightFacing - bodyFacing).Cos() / (1048576 * aircraft.Info.Speed)));
				roll += new WAngle((int)(forw * (flightFacing - bodyFacing).Sin() / (1048576 * aircraft.Info.Speed)));
			}

			// Determine new displacement vector.
			var move = aircraft.FlyStep(aircraft.CurrentSpeed, new WRot(WAngle.Zero, aircraft.FlightPitch, aircraft.FlightFacing));

			// Lock in new body and flight attitudes and velocities.
			aircraft.FlightFacing = flightFacing;
			aircraft.FlightPitch = flightPitch;
			aircraft.Orientation = new WRot(roll, pitch, bodyFacing);
			aircraft.CurrentSpeed = speed;
			aircraft.CurrentVelocity = move;
			aircraft.CurrentFlightTurnSpeed = flightTurnSpeed;
			aircraft.CurrentBodyTurnSpeed = bodyTurnSpeed;
			aircraft.SetPosition(self, aircraft.CenterPosition + move);
		}

		// Should only be used for vertical-only movement, usually VTOL take-off or land. Terrain-induced altitude changes
		// should always be handled by FlyTick.
		public static void HoverTick(Actor self, Aircraft aircraft, WPos? destination = null, WAngle? desiredFacing = null)
		{
			var currentPos = aircraft.GetPosition();
			var dat = self.World.Map.DistanceAboveTerrain(aircraft.CenterPosition);

			var desiredPos = destination ?? currentPos + WVec.FromZ(aircraft.Info.CruiseAltitude - dat);
			var delta = desiredPos - currentPos;

			var currentVelocity = aircraft.CurrentVelocity;
			var maxAccel = aircraft.VTOLAcceleration.Length;

			// Make sure we decelerate in time.
			var desiredVelocity = delta;
			if (delta != WVec.Zero)
			{
				var maxAccelVec = delta * maxAccel / delta.Length;

				var desiredVelocityX = delta.X;
				if (maxAccelVec.X * currentVelocity.X > 0 && Math.Abs(delta.X) < currentVelocity.X * currentVelocity.X / Math.Abs(maxAccelVec.X )/ 2)
					desiredVelocityX = 0;

				var desiredVelocityY = delta.Y;
				if (maxAccelVec.Y * currentVelocity.Y > 0 && Math.Abs(delta.Y) < currentVelocity.Y * currentVelocity.Y / Math.Abs(maxAccelVec.Y) / 2)
					desiredVelocityY = 0;

				var desiredVelocityZ = delta.Z;
				if (maxAccelVec.Z * currentVelocity.Z > 0 && Math.Abs(delta.Z) < currentVelocity.Z * currentVelocity.Z / Math.Abs(maxAccelVec.Z) / 2)
					desiredVelocityZ = 0;

				desiredVelocity = new WVec(desiredVelocityX, desiredVelocityY, desiredVelocityZ);
				if (desiredVelocity != WVec.Zero)
				{
					var desiredSpeed = desiredVelocity.Length;
					desiredVelocity = desiredVelocity * Math.Min(aircraft.Info.AltitudeVelocity.Length, desiredSpeed) / desiredSpeed;
				}
			}

			// Acceleration
			var acceleration = desiredVelocity - currentVelocity;
			if (acceleration != WVec.Zero)
			{
				var accelLength = acceleration.Length;
				acceleration = acceleration * Math.Min(maxAccel, accelLength) / accelLength;
			}

			var velocity = currentVelocity + acceleration;

			// Independently turn the aircraft body. While landing, every aircraft behaves like it can slide.
			var desiredBodyTurnSpeed = WAngle.Zero;
			if (desiredFacing.HasValue)
			{
				var bodyFacingDelta = desiredFacing.Value - aircraft.Facing;
				if (Math.Abs(bodyFacingDelta.Angle2) >= aircraft.CurrentBodyTurnSpeed.Angle2 * aircraft.CurrentBodyTurnSpeed.Angle2 / aircraft.BodyTurnAcceleration.Angle2 / 2)
					desiredBodyTurnSpeed = bodyFacingDelta.Clamp(-aircraft.BodyTurnSpeed, aircraft.BodyTurnSpeed);
			}

			var bodyTurnSpeed = Util.TickFacing(aircraft.CurrentBodyTurnSpeed, desiredBodyTurnSpeed, aircraft.BodyTurnAcceleration);
			var bodyFacing = aircraft.Facing + bodyTurnSpeed;

			// Rotate body to landing position.
			var bodyPitch = Util.TickFacing(aircraft.Pitch, WAngle.Zero, aircraft.Info.PitchSpeed);
			var bodyRoll = Util.TickFacing(aircraft.Roll, WAngle.Zero, aircraft.Info.RollSpeed);

			// Lock in new body and flight attitudes and velocities.
			aircraft.FlightFacing = aircraft.Facing;
			aircraft.FlightPitch = aircraft.Pitch;
			aircraft.Orientation = new WRot(bodyRoll, bodyPitch, bodyFacing);
			aircraft.CurrentVelocity = velocity;
			aircraft.CurrentSpeed = 0;
			aircraft.CurrentFlightTurnSpeed = WAngle.Zero;
			aircraft.CurrentBodyTurnSpeed = bodyTurnSpeed;
			aircraft.SetPosition(self, aircraft.CenterPosition + velocity);
		}

		public override bool Tick(Actor self)
		{
			// Refuse to take off if it would land immediately again.
			if (aircraft.ForceLanding)
				Cancel(self);

			var dat = self.World.Map.DistanceAboveTerrain(aircraft.CenterPosition);
			var isLanded = dat <= aircraft.LandAltitude;

			// HACK: Prevent paused (for example, EMP'd) aircraft from taking off.
			// This is necessary until the TODOs in the IsCanceling block below are adressed.
			if (isLanded && aircraft.IsTraitPaused)
				return false;

			if (IsCanceling)
			{
				// We must return the actor to a sensible height before continuing.
				// If the aircraft is on the ground we queue TakeOff to manage the influence reservation and takeoff sounds etc.
				// TODO: It would be better to not take off at all, but we lack the plumbing to detect current airborne/landed state.
				// If the aircraft lands when idle and is idle, we let the default idle handler manage this.
				// TODO: Remove this after fixing all activities to work properly with arbitrary starting altitudes.
				var landWhenIdle = aircraft.Info.IdleBehavior == IdleBehaviorType.Land;
				var skipHeightAdjustment = landWhenIdle && self.CurrentActivity.IsCanceling && self.CurrentActivity.NextActivity == null;
				if (aircraft.Info.CanHover && !skipHeightAdjustment && dat != aircraft.Info.CruiseAltitude)
				{
					if (isLanded)
						QueueChild(new TakeOff(self));
					else
						HoverTick(self, aircraft);

					return false;
				}

				return true;
			}
			else if (isLanded)
			{
				QueueChild(new TakeOff(self));
				return false;
			}

			bool targetIsHiddenActor;
			target = target.Recalculate(self.Owner, out targetIsHiddenActor);
			if (!targetIsHiddenActor && target.Type == TargetType.Actor)
				lastVisibleTarget = Target.FromTargetPositions(target);

			useLastVisibleTarget = targetIsHiddenActor || !target.IsValidFor(self);

			// Target is hidden or dead, and we don't have a fallback position to move towards
			if (useLastVisibleTarget && !lastVisibleTarget.IsValidFor(self))
				return true;

			var checkTarget = useLastVisibleTarget ? lastVisibleTarget : target;
			var pos = aircraft.GetPosition();
			var delta = checkTarget.CenterPosition - pos;

			// Inside the target annulus, so we're done
			var insideMaxRange = maxRange.Length > 0 && checkTarget.IsInRange(pos, maxRange);
			var insideMinRange = minRange.Length > 0 && checkTarget.IsInRange(pos, minRange);
			if (insideMaxRange && !insideMinRange)
				return true;

			var isSlider = aircraft.Info.CanSlide;
			var desiredFacing = delta.HorizontalLengthSquared != 0 ? delta.Yaw : aircraft.FlightFacing;

			// Inside the minimum range, so reverse if we CanSlide, otherwise face away from the target.
			if (insideMinRange)
			{
				FlyTick(self, aircraft, desiredFacing: desiredFacing + new WAngle(512), desiredBodyFacing: desiredFacing);
				return false;
			}

			// Calculate intermediate variables for deceleration.
			var speed = aircraft.CurrentSpeed;
			var accel = aircraft.Acceleration;
			var speedDelta = speed - finalSpeed;
			var brakeTime = speedDelta / accel;
			var parBrakeDist = speedDelta * speedDelta / accel / 2;

			// If we can slide we should start turning to reach the desired facing at the last possible moment.
			WAngle? desiredBodyFacing = null;
			if (aircraft.Info.CanSlide && finalFacing.HasValue)
			{
				var turnSpeed = aircraft.BodyTurnSpeed.Angle;
				var turnTime = Math.Abs((finalFacing - aircraft.Facing).Value.Angle2) / turnSpeed + turnSpeed / aircraft.BodyTurnAcceleration.Angle;
				var turnDist = speed * turnTime - (turnTime < brakeTime ? accel * turnTime * turnTime / 2 : parBrakeDist);
				if (turnDist >= delta.HorizontalLength)
					desiredBodyFacing = finalFacing;
			}

			// HACK: Consider ourselves blocked if we have moved by less than sqrt(15) * the smallest possible deliberate displacement
			// in the last five ticks. Stop if we are blocked and close enough
			// HACK: sqrt(15) is empirically determined and completely arbitrary.
			if (positionBuffer.Count >= 5 && (positionBuffer.Last() - positionBuffer[0]).LengthSquared < 15 * accel * accel &&
				delta.HorizontalLengthSquared <= nearEnough.LengthSquared)
				return true;

			// The next move would overshoot, so consider it close enough.
			if (delta.HorizontalLength <= aircraft.MovementSpeed)
				return true;

			int desiredSpeed = -1;
			var flightTurnSpeed = aircraft.TurnSpeed;
			if (flightTurnSpeed.Angle < 512)
			{
				// Using the turn rate, compute a hypothetical circle traced by a continuous turn.
				// If it contains the destination point, it's unreachable without more complex manuvering.
				var turnRadius = CalculateTurnRadius(speed, flightTurnSpeed);

				// The current facing is a tangent of the minimal turn circle.
				// Make a perpendicular vector, and use it to locate the turn's center.
				var turnCenterFacing = aircraft.FlightFacing + new WAngle(Util.GetTurnDirection(aircraft.FlightFacing, desiredFacing) * 256);
				var turnCenterDir = new WVec(0, -1024, 0).Rotate(WRot.FromYaw(turnCenterFacing));

				// Compare with the target point, and slow down if it's inside the circle.
				var turnCenter = aircraft.CenterPosition + turnCenterDir * turnRadius / 1024;
				if ((checkTarget.CenterPosition - turnCenter).HorizontalLengthSquared < turnRadius * turnRadius)
				{
					turnRadius = CalculateTurnRadius(finalSpeed, flightTurnSpeed);
					turnCenter = aircraft.CenterPosition + turnCenterDir * turnRadius / 1024;

					// If we are not allowed to slow down enough, we keep flying away instead.
					if ((checkTarget.CenterPosition - turnCenter).HorizontalLengthSquared < turnRadius * turnRadius)
						desiredFacing = aircraft.FlightFacing;
					else
						desiredSpeed = finalSpeed;
				}
				else
				{
					turnRadius = CalculateTurnRadius(speed + aircraft.Acceleration, flightTurnSpeed);
					turnCenter = aircraft.CenterPosition + turnCenterDir * turnRadius / 1024;

					// The next acceleration step would cause the target point to be within the turn circle
					// So we keep our current speed instead.
					if ((checkTarget.CenterPosition - turnCenter).HorizontalLengthSquared < turnRadius * turnRadius)
						desiredSpeed = speed;
				}
			}

			// Determine when we should start to slow down.
			if (delta.HorizontalLength < speed * speedDelta / accel - parBrakeDist)
				desiredSpeed = finalSpeed;

			positionBuffer.Add(self.CenterPosition);
			if (positionBuffer.Count > 5)
				positionBuffer.RemoveAt(0);

			FlyTick(self, aircraft, desiredFacing: desiredFacing, desiredSpeed: desiredSpeed,
				desiredBodyFacing: desiredBodyFacing);

			return false;
		}

		public override IEnumerable<Target> GetTargets(Actor self)
		{
			yield return target;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (targetLineColor.HasValue)
				yield return new TargetLineNode(useLastVisibleTarget ? lastVisibleTarget : target, targetLineColor.Value);
		}

		public static int CalculateTurnRadius(int speed, WAngle turnSpeed)
		{
			// turnSpeed -> divide into 256 to get the number of ticks per complete rotation
			// speed -> multiply to get distance travelled per rotation (circumference)
			// 180 -> divide by 2*pi to get the turn radius: 180==1024/(2*pi), with some extra leeway
			return turnSpeed.Angle > 0 ? 180 * speed / turnSpeed.Angle : 0;
		}
	}
}
