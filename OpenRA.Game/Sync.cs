#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Traits;

namespace OpenRA
{
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public sealed class SyncAttribute : Attribute { }

	[RequireExplicitImplementation]
	public interface ISync
	{
		int GetSyncHash() => throw new NotImplementedException();
	}

	public static class Sync
	{
		public static void RunUnsynced(World world, Action fn)
		{
			RunUnsynced(Game.Settings.Debug.SyncCheckUnsyncedCode, world, () => { fn(); return true; });
		}

		public static void RunUnsynced(bool checkSyncHash, World world, Action fn)
		{
			RunUnsynced(checkSyncHash, world, () => { fn(); return true; });
		}

		static int unsyncCount = 0;

		public static T RunUnsynced<T>(World world, Func<T> fn)
		{
			return RunUnsynced(Game.Settings.Debug.SyncCheckUnsyncedCode, world, fn);
		}

		public static T RunUnsynced<T>(bool checkSyncHash, World world, Func<T> fn)
		{
			unsyncCount++;

			// Detect sync changes in top level entry point only. Do not recalculate sync hash during reentry.
			var sync = unsyncCount == 1 && checkSyncHash && world != null ? world.SyncHash() : 0;

			// Running this inside a try with a finally statement means unsyncCount is decremented as soon as fn completes
			try
			{
				return fn();
			}
			finally
			{
				unsyncCount--;

				// When the world is disposing all actors and effects have been removed
				// So do not check the hash for a disposing world since it definitively has changed
				if (unsyncCount == 0 && checkSyncHash && world != null && !world.Disposing && sync != world.SyncHash())
					throw new InvalidOperationException("RunUnsynced: sync-changing code may not run here");
			}
		}

		public static void AssertUnsynced(string message)
		{
			if (unsyncCount == 0)
				throw new InvalidOperationException(message);
		}
	}
}
