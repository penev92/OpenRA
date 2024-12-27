using System.IO;
using System.Text;
using OpenRA.FileSystem;

namespace OpenRA.Mods.Common.Campaign
{
	// TODO: Making this "an IGlobalModData" seems hacky!
	public class CampaignManager : IGlobalModData
	{
		public void StartNewCampaign(string modId, string selectedCampaign)
		{
			var resolvedFolder = Platform.ResolvePath(Path.Combine(Platform.SupportDir, "Campaigns", modId));
			if (!Directory.Exists(resolvedFolder))
				Directory.CreateDirectory(resolvedFolder);

			var combinedPath = Path.Combine(resolvedFolder, $"{selectedCampaign}.zip");
			var campaignPackage = ZipFileLoader.Create(combinedPath);
			campaignPackage.Update("campaign.json", Encoding.UTF8.GetBytes($"{{\"campaignId\": \"{selectedCampaign}\"}}"));


		}
	}
}
