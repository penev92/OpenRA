 #region Copyright & License Information
 /*
  * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
  * This file is part of OpenRA, which is free software. It is made
  * available to you under the terms of the GNU General Public License
  * as published by the Free Software Foundation. For more information,
  * see COPYING.
  */
 #endregion

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Actors with the \"ClonesProducedUnits\" trait will produce a free duplicate of me.")]
	public class CloneableInfo : TraitInfo<Cloneable>
	{
		[Desc("This unit's cloneable type is:")]
		public readonly string[] Types = { };
	}

	public class Cloneable { }
}
