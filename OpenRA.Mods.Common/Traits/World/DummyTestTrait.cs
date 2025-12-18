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

using OpenRA.FastFieldLoading;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static OpenRA.ObjectCreator;

namespace OpenRA.Mods.Common.Traits
{
	public partial class DummyTestTraitInfo
	{
		// TODO: Handle RequiredAttribute!
		// TODO: Handle LoadUsingAttribute!
		[UseCtor]
		public DummyTestTraitInfo(Dictionary<string, MiniYaml> miniYamlDictionary)
		{
			IsLoaded = FastFieldLoader.Load<bool, SpanParsableParser<bool>>(miniYamlDictionary, nameof(IsLoaded), IsLoaded);
			OverrideShroudIndex = FastFieldLoader.Load<int, IntegerParser<int>>(miniYamlDictionary, nameof(OverrideShroudIndex), OverrideShroudIndex);
			Sequence = FastFieldLoader.LoadString(miniYamlDictionary, nameof(Sequence), Sequence);
			ShroudBlend = FastFieldLoader.Load<BlendMode, EnumParser<BlendMode>>(miniYamlDictionary, nameof(ShroudBlend), ShroudBlend);
			PointInSpace = FastFieldLoader.Load<float3, SpanParsableParser<float3>>(miniYamlDictionary, nameof(PointInSpace), PointInSpace);
			Index = FastFieldLoader.LoadArray<int, IntegerParser<int>>(miniYamlDictionary, nameof(Index), Index);
			ShroudVariants = FastFieldLoader.LoadArray(miniYamlDictionary, nameof(ShroudVariants), ShroudVariants);
			ManyPoints = FastFieldLoader.LoadArray<int2, SpanParsableParser<int2>>(miniYamlDictionary, nameof(ManyPoints), ManyPoints, 2);
			TerrainTypes = FastFieldLoader.LoadHashSet(miniYamlDictionary, nameof(TerrainTypes), TerrainTypes);
			TerrainSpeeds = FastFieldLoader.LoadDictionary<string, int, SpanParsableParser<string>, SpanParsableParser<int>>(
				miniYamlDictionary, nameof(TerrainSpeeds), TerrainSpeeds);
			DamageModifiers = FastFieldLoader.LoadDictionary<int, float3, SpanParsableParser<int>, SpanParsableParser<float3>>(
				miniYamlDictionary, nameof(DamageModifiers), DamageModifiers);

			//IsLoaded = FieldLoader.LoadBool(miniYamlDictionary, nameof(IsLoaded), IsLoaded);
			//OverrideShroudIndex = FieldLoader.LoadNumeric(miniYamlDictionary, nameof(OverrideShroudIndex), NumberStyles.Integer, OverrideShroudIndex);
			//Sequence = FieldLoader.LoadString(miniYamlDictionary, nameof(Sequence), Sequence);
			//PointInSpace = FieldLoader.LoadFloat3(miniYamlDictionary, nameof(PointInSpace), PointInSpace);
			//ShroudBlend = FieldLoader.LoadEnum(miniYamlDictionary, nameof(ShroudBlend), ShroudBlend);
			//Index = FieldLoader.LoadNumericArray(miniYamlDictionary, nameof(Index), NumberStyles.Integer, Index) ?? [];
			//ShroudVariants = FieldLoader.LoadStringArray(miniYamlDictionary, nameof(ShroudVariants), ShroudVariants) ?? [];
			//ManyPoints = FieldLoader.LoadInt2Array(miniYamlDictionary, nameof(ManyPoints), ManyPoints) ?? [];
			//TerrainTypes = ?? [];
			//DamageModifiers = ?? [];

			//if (miniYamlDictionary.TryGetValue(nameof(IsLoaded), out var isLoadedNode))
			//{
			//	if (bool.TryParse(isLoadedNode.Value, out var isLoaded))
			//		IsLoaded = isLoaded;
			//	else
			//		FieldLoader.InvalidValueAction(isLoadedNode.Value, IsLoaded.GetType(), nameof(IsLoaded));
			//}

			//if (miniYamlDictionary.TryGetValue(nameof(OverrideShroudIndex), out var overrideShroudIndexNode))
			//{
			//	if (Exts.TryParseInt32Invariant(overrideShroudIndexNode.Value, out var overrideShroudIndex))
			//		OverrideShroudIndex = overrideShroudIndex;
			//	else
			//		FieldLoader.InvalidValueAction(overrideShroudIndexNode.Value, OverrideShroudIndex.GetType(), nameof(OverrideShroudIndex));
			//}

			//if (miniYamlDictionary.TryGetValue(nameof(Sequence), out var sequenceNode))
			//	Sequence = sequenceNode.Value;

			//if (miniYamlDictionary.TryGetValue(nameof(ShroudBlend), out var shroudBlendNode))
			//{
			//	if (Enum.TryParse<BlendMode>(shroudBlendNode.Value, out var shroudBlend))
			//		ShroudBlend = shroudBlend;
			//	else
			//		FieldLoader.InvalidValueAction(shroudBlendNode.Value, ShroudBlend.GetType(), nameof(ShroudBlend));
			//}

			//if (miniYamlDictionary.TryGetValue(nameof(Index), out var indexNode))
			//{
			//	Index = indexNode.Value
			//		?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			//		?.Select(x => int.Parse(x, NumberStyles.Integer, NumberFormatInfo.InvariantInfo))
			//		.ToArray()
			//		?? Array.Empty<int>();
			//}

			//if (miniYamlDictionary.TryGetValue(nameof(ShroudVariants), out var shroudVariantsNode))
			//	ShroudVariants = shroudVariantsNode.Value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
		}
	}

	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	public partial class DummyTestTraitInfo : TraitInfo
	{
		public readonly bool IsLoaded = true;

		public readonly int OverrideShroudIndex = 15;

		public readonly string Sequence = "shroud";

		public readonly BlendMode ShroudBlend = BlendMode.Alpha;

		public readonly float3 PointInSpace;

		public readonly int[] Index = { 12, 9, 8, 3, 1, 6, 4, 2, 13, 11, 7, 14 };

		public readonly string[] ShroudVariants = { "shroud" };

		public readonly int2[] ManyPoints = [new(1, 2), new(4, 5)];

		public readonly HashSet<string> TerrainTypes = [];

		public readonly Dictionary<string, int> TerrainSpeeds = [];

		public readonly Dictionary<int, float3> DamageModifiers = [];

		public override object Create(ActorInitializer init) { return new DummyTestTrait(); }
	}

	public sealed class DummyTestTrait
	{
	}
}
