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
using ICSharpCode.SharpZipLib.Zip;

namespace OpenRA.FileSystem
{
	public sealed class MicrosoftCABFile : IFolder, IDisposable
	{
		struct CFHeader
		{
			public uint cbCabinet;		/* size of this cabinet file in bytes */
			public uint coffFiles;		/* absolute offset of the first CFFILE entry */
			public byte versionMinor;	/* cabinet file format version, minor */
			public byte versionMajor;	/* cabinet file format version, major */
			public ushort cFolders;		/* number of CFFOLDER entries in this cabinet */
			public ushort cFiles;		/* number of CFFILE entries in this cabinet */
			public ushort flags;        /* cabinet file option indicators */
			public ushort setID;        /* must be the same for all cabinets in a set */
			public ushort iCabinet;		/* number of this cabinet file in a set */
			public ushort cbCFHeader;	/* (optional) size of per-cabinet reserved area */
			public byte cbCFFolder;			/* (optional) size of per-folder reserved area */
			public byte cbCFData;			/* (optional) size of per-datablock reserved area */
			public byte[] abReserve;		/* (optional) per-cabinet reserved area */
			public byte[] szCabinetPrev;	/* (optional) name of previous cabinet file */
			public byte[] szDiskPrev;		/* (optional) name of previous disk */
			public byte[] szCabinetNext;	/* (optional) name of next cabinet file */
			public byte[] szDiskNext;		/* (optional) name of next disk */

			public CFHeader(BinaryReader reader)
			{
				cbCFHeader = 0;
				cbCFFolder = 0;
				cbCFData = 0;
				szCabinetPrev = szDiskPrev = szCabinetNext = szDiskNext = new byte[0];

				// Reserved field, set to zero.
				reader.ReadInt32();
				cbCabinet = reader.ReadUInt32();

				// Reserved field, set to zero.
				reader.ReadInt32();
				coffFiles = reader.ReadUInt32();

				// Reserved field, set to zero.
				reader.ReadInt32();
				versionMinor = reader.ReadByte();
				versionMajor = reader.ReadByte();
				cFolders = reader.ReadUInt16();
				cFiles = reader.ReadUInt16();

				flags = reader.ReadUInt16();
				var hasPreviousCabinet = (flags & 1) != 0;
				var hasNextCabinet = (flags & 2) != 0;
				var hasReserved = (flags & 4) != 0;

				setID = reader.ReadUInt16();
				iCabinet = reader.ReadUInt16();

				if (hasReserved)
				{
					cbCFHeader = reader.ReadUInt16();
					cbCFFolder = reader.ReadByte();
					cbCFData = reader.ReadByte();
				}

				abReserve = reader.ReadBytes(cbCFHeader);

				if (hasPreviousCabinet)
				{
					szCabinetPrev = reader.ReadBytes(255);
					szDiskPrev = reader.ReadBytes(255);

				}

				if (hasNextCabinet)
				{
					szCabinetNext = reader.ReadBytes(255);
					szDiskNext = reader.ReadBytes(255);
				}
			}
		}

		struct CFFolder
		{
			public uint coffCabStart;  /* absolute offset of the first CFDATA block in this folder */
			public ushort cCFData;       /* number of CFDATA blocks in this folder */
			ushort typeCompress;  /* compression type indicator */
			byte[] abReserve;   /* (optional) per-folder reserved area */

			public CFFolder(BinaryReader reader, byte cbCFFolder)
			{
				coffCabStart = reader.ReadUInt32();
				cCFData = reader.ReadUInt16();
				typeCompress = reader.ReadUInt16();
				abReserve = reader.ReadBytes(cbCFFolder);
			}
		}

		struct CFFile
		{
			public string FileName;

			uint cbFile;           /* uncompressed size of this file in bytes */
			uint uoffFolderStart;  /* uncompressed offset of this file in the folder */
			ushort iFolder;          /* index into the CFFOLDER area */
			ushort date;             /* date stamp for this file */
			ushort time;             /* time stamp for this file */
			ushort attribs;          /* attribute flags for this file */

			public CFFile(BinaryReader reader)
			{
				cbFile = reader.ReadUInt32();
				uoffFolderStart = reader.ReadUInt32();
				iFolder = reader.ReadUInt16();
				date = reader.ReadUInt16();
				time = reader.ReadUInt16();
				attribs = reader.ReadUInt16();

				var name = new List<byte>();
				while (true)
				{
					var current = reader.ReadByte();
					if (current == '\0')
						break;

					name.Add(current);
				}
				FileName = System.Text.Encoding.UTF8.GetString(name.ToArray());
			}
		}

		struct CFData
		{
			uint csum;			/* checksum of this CFDATA entry */
			ushort cbData;       /* number of compressed bytes in this block */
			ushort cbUncomp;     /* number of uncompressed bytes in this block */
			byte[] abReserve;	/* (optional) per-datablock reserved area */
			public byte[] Data;			/* compressed data bytes */

			public CFData(BinaryReader reader, ushort cbCFHeader, byte cbCFData)
			{
				csum = reader.ReadUInt32();
				cbData = reader.ReadUInt16();
				cbUncomp = reader.ReadUInt16();

				abReserve = reader.ReadBytes(cbCFData);
				Data = reader.ReadBytes(cbData);
				var str = string.Concat(new[] { (char)Data[0], (char)Data[1] });
				if (str != "CK")
				//if (Data[0] != 0x43 || Data[1] != 0x4B)
					csum = 0;
			}
		}

		string filename;

		public MicrosoftCABFile(string filename)
		{
			var stream = GlobalFileSystem.Open(filename);
			var reader = new BinaryReader(stream);

			var signature = reader.ReadChars(4);
			if (string.Concat(signature) != "MSCF")
				throw new InvalidDataException("Not a Microsoft CAB package!");

			var header = new CFHeader(reader);

			var cfFolders = new List<CFFolder>();
			for (var i = 0; i < header.cFolders; i++)
				cfFolders.Add(new CFFolder(reader, header.cbCFFolder));

			var cfFiles = new List<CFFile>();
			stream.Seek(header.coffFiles, SeekOrigin.Begin);
			for (var i = 0; i < header.cFiles; i++)
				cfFiles.Add(new CFFile(reader));

			var fileData = new List<List<byte>>();
			foreach (var folder in cfFolders)
			{
				stream.Seek(folder.coffCabStart, SeekOrigin.Begin);
				var data = new List<byte>();
				for (var i = 0; i < folder.cCFData; i++)
				{
					var cfdata = new CFData(reader, header.cbCFHeader, header.cbCFData);
					data.AddRange(cfdata.Data);
				}

				fileData.Add(data);
			}

			var zips = new List<ZipFile>();

			var fileNum = 0;
			foreach (var data in fileData)
			{
				var zipname = "asdf" + fileNum++ + ".zip";
				var zip = new ZipFile(zipname, 0, new Dictionary<string, byte[]> { { "all", data.ToArray() } });

				var memStream = new MemoryStream(data.ToArray());

				var input = new ZipInputStream(memStream);
				//var output = new ZipOutputStream(File.Open("output.zip", FileMode.CreateNew));

				var stream2 = zip.GetContent(zipname);
			}
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
