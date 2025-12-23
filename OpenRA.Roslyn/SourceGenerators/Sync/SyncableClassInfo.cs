namespace OpenRA.Roslyn.SourceGenerators.Sync
{
	record struct SyncableClassInfo(
		string NamespaceName,
		string ClassName,
		string ClassModifiers);
}
