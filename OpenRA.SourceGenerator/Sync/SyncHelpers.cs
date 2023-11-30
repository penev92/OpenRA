using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace OpenRA.SourceGenerators.Sync
{
	static class SyncHelpers
	{
		public const string SyncAttributeName = "SyncAttribute";
		public const string SyncInterfaceName = "OpenRA.ISync";

		public static string GetHashCodeString(string typeName, string objectName, out bool isTarget)
		{
			isTarget = typeName == "Target";
			return typeName switch
			{
				"Boolean" => $"({objectName} ? 1 : 0)",
				"Int32" => objectName,
				"int2" => $"((({objectName}.X * 5) ^ ({objectName}.Y * 3)) / 4)",
				"CPos" => $"{objectName}.Bits",
				"CVec" => $"((({objectName}.X * 5) ^ ({objectName}.Y * 3)) / 4)",
				"WDist" => $"{objectName}.Length",
				"WPos" => $"{objectName}.X ^ {objectName}.Y ^ {objectName}.Z",
				"WVec" => $"{objectName}.X ^ {objectName}.Y ^ {objectName}.Z",
				"WAngle" => $"{objectName}.Angle",
				"WRot" => $"{objectName}.Roll ^ {objectName}.Pitch ^ {objectName}.Yaw",
				"Actor" => $"({objectName} == null ? 0 : (int)({objectName}.ActorID << 16))",
				"Player" => $"({objectName} == null ? 0 : (int)({objectName}.PlayerActor.ActorID << 16) * 0x567)",
				"Target" => $"HashTarget({objectName})",
				_ => throw new NotSupportedException()
			};
		}

		public static bool ImplementsISync(this INamedTypeSymbol type) => type.AllInterfaces.Any(y => y.ToDisplayString() == SyncInterfaceName);

		public static bool HasSyncAttribute(ISymbol symbol) => symbol.GetAttributes().Any(x => x.AttributeClass.Name == SyncAttributeName);

		public static string GetTargetSyncHashMethod() => $@"
		static int HashTarget(Target t)
		{{
			switch (t.Type)
			{{
				case TargetType.Actor:
					return {GetHashCodeString("Actor", "t.Actor", out _)};

				case TargetType.FrozenActor:
					var actor = t.FrozenActor.Actor;
					if (actor == null)
						return 0;

					return {GetHashCodeString("Actor", "actor", out _)};

				case TargetType.Terrain:
					return {GetHashCodeString("WPos", "t.CenterPosition", out _)};

				case TargetType.Invalid:
				default:
					return 0;
			}}
		}}";
	}
}
