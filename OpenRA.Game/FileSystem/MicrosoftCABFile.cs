#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;

namespace OpenRA.FileSystem
{
	public sealed class MicrosoftCABFile : IFolder, IDisposable
	{
		string filename;

		public MicrosoftCABFile(string filename)
		{
			var stream = GlobalFileSystem.Open(filename);
			var reader = new BinaryReader(stream);

			var signature = reader.ReadChars(4);
			if (string.Concat(signature) != "MSCF")
				throw new InvalidDataException("Not a Microsoft CAB package!");
		}

		public Stream GetContent(string filename)
		{
			return null;
		}

		public IEnumerable<uint> ClassicHashes()
		{
			yield break;
		}

		public IEnumerable<uint> CrcHashes()
		{
			yield break;
		}

		public IEnumerable<string> AllFileNames()
		{
			yield break;
		}

		public bool Exists(string filename)
		{
			return false;
			//return pkg.GetEntry(filename) != null;
		}

		public int Priority { get { return 500 + 0; } }
		public string Name { get { return filename; } }

		public void Write(Dictionary<string, byte[]> contents)
		{
			throw new NotImplementedException("Cannot save CAB archives.");
		}

		public void Dispose()
		{
		}
	}
}
