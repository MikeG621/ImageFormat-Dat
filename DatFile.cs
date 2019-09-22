/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LA *.DAT Image files
 * Copyright (C) 2009-2019 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * This library is free software; you can redistribute it and/or modify it
 * under the terms of the Mozilla Public License; either version 2.0 of the
 * License, or (at your option) any later version.
 *
 * This library is "as is" without warranty of any kind; including that the
 * library is free of defects, merchantable, fit for a particular purpose or
 * non-infringing. See the full license text for more details.
 *
 * If a copy of the MPL (MPL.txt) was not distributed with this file,
 * you can obtain one at http://mozilla.org/MPL/2.0/.
 *
 * VERSION: 2.2
 */

/* CHANGE LOG
 * v2.2, 190922
 * [UPD] tweaked some comments
 * [NEW] UsedHeight, UsedWidth
 * v2.1, 141214
 * [UPD] switch to MPL
 * v2.0, 120505
 * [FIX] _valEx now says Dat instead of Act >.<
 * [DEL] _groupsIndexer
 * [UPD] Groups now simple prop, Groups.set is internal
 * [UPD] Group/Sub converted to class
*/

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object to work with *.DAT image archives found in XWA</summary>
	public class DatFile
	{
		string _filePath = "newfile.dat";
		internal static string _valEx = "Validation error, file is not a LucasArts Dat Image file or is corrupted.";
		const long _validationID = 0x5602235657062357;

		#region constructors
		/// <summary>Creates a blank Dat archive</summary>
		public DatFile()
		{
			Groups = new GroupCollection();
		}
		/// <summary>Loads an existing Dat archive</summary>
		/// <param name="file">Full path to the *.dat file</param>
		/// <exception cref="LoadFileException">File cannot be loaded</exception>
		public DatFile(string file)
		{
			FileStream fs = null;
			try
			{
				if (!file.ToUpper().EndsWith(".DAT")) throw new ArgumentException("Invalid file extension, must be *.DAT", "file");
				fs = File.OpenRead(file);
				DecodeFile(new BinaryReader(fs).ReadBytes((int)fs.Length));
				fs.Close();
			}
			catch (Exception x)
			{
				if (fs != null) fs.Close();
				throw new LoadFileException(x);
			}
			_filePath = file;
		}
		#endregion constructors

		#region public methods
		/// <summary>Save the Dat archive in its existing location</summary>
		/// <exception cref="SaveFileException">File cannot be saved</exception>
		/// <remarks>Original remains unchanged on failure</remarks>
		public void Save()
		{
			FileStream fs = null;
			string tempFile = _filePath + ".tmp";
			try
			{
				if (NumberOfGroups < 1) throw new InvalidDataException("No Groups defined");
				for (int g = 0; g < NumberOfGroups; g++) if (Groups[g].ID < 0) throw new InvalidDataException("Not all Groups have been initialized");
				_updateGroupHeaders();
				for (int g = 0; g < NumberOfGroups; g++)
					for (int s = 0; s < Groups[g].NumberOfSubs; s++) Groups[g].Subs[s]._headerUpdate();
				if (File.Exists(_filePath)) File.Copy(_filePath, tempFile);	// create backup
				File.Delete(_filePath);
				fs = File.OpenWrite(_filePath);
				BinaryWriter bw = new BinaryWriter(fs);
				// FileHeader
				bw.Write(_validationID);
				bw.Write((short)1);
				bw.Write(NumberOfGroups);
				bw.Write(Groups.NumberOfSubs);
				bw.Write(_length);
				bw.Write(Groups.NumberOfColors);
				fs.Position += 8;	// long 0
				bw.Write(_dataOffset);
				for (int g = 0; g < NumberOfGroups; g++) bw.Write(Groups[g]._header);
				// Groups
				for (int g = 0; g < NumberOfGroups; g++)
				{
					// Subs
					for (int s = 0; s < Groups[g].NumberOfSubs; s++)
					{
						bw.Write(Groups[g].Subs[s]._headers);
						for (int c = 0; c < Groups[g].Subs[s].NumberOfColors; c++)
						{
							bw.Write(Groups[g].Subs[s].Colors[c].R);
							bw.Write(Groups[g].Subs[s].Colors[c].G);
							bw.Write(Groups[g].Subs[s].Colors[c].B);
						}
						bw.Write(Groups[g].Subs[s]._rows);
					}
				}
				fs.SetLength(fs.Position);
				fs.Close();
				File.Delete(tempFile);	// delete backup if it exists
			}
			catch (Exception x)
			{
				if (fs != null) fs.Close();
				if (File.Exists(tempFile)) File.Copy(tempFile, _filePath);	// restore backup if it exists
				File.Delete(tempFile);	// delete backup if it exists
				throw new SaveFileException(x);
			}
		}

		/// <summary>Save the Dat archive in a new location</summary>
		/// <param name="file">Full path to the new location</param>
		/// <exception cref="SaveFileException">File cannot be saved</exception>
		/// <remarks>Original remains unchanged on failure</remarks>
		public void Save(string file)
		{
			_filePath = file;
			Save();
		}

		/// <summary>Populates the Dat with the raw byte data from file</summary>
		/// <param name="rawData">Entire contents of a *.DAT archive</param>
		/// <exception cref="ArgumentException">Validation error</exception>
		public void DecodeFile(byte[] rawData)
		{
			// Dat.FileHeader
			if (BitConverter.ToInt64(rawData, 0) != _validationID) throw new ArgumentException(_valEx, "file");
			if (BitConverter.ToInt16(rawData, 8) != 1) throw new ArgumentException(_valEx, "file");
			short numberOfGroups = BitConverter.ToInt16(rawData, 0xA);
			int offset = 0x22;
			// Dat.GroupHeaders
			Groups = new GroupCollection(numberOfGroups);
			byte[] header = new byte[Group._headerLength];
			for (int i = 0; i < numberOfGroups; i++)
			{
				ArrayFunctions.TrimArray(rawData, offset, header);
				Groups[i] = new Group(header);
				offset += Group._headerLength;
			}
			// Dat.Groups
			for (int i = 0; i < numberOfGroups; i++)
			{	// Group.Subs
				for (int j = 0; j < Groups[i].NumberOfSubs; j++)
				{
					int length = BitConverter.ToInt32(rawData, offset + 0xE);
					byte[] sub = new byte[length + Sub._subHeaderLength];
					ArrayFunctions.TrimArray(rawData, offset, sub);
					Groups[i].Subs[j] = new Sub(sub);
					offset += sub.Length;
				}
			}
		}
		#endregion public methods
		
		#region public properties
		/// <summary>Gets the file name of the Dat object</summary>
		/// <remarks>Value is <see cref="FilePath"/> without the directory</remarks>
		public string FileName { get { return StringFunctions.GetFileName(_filePath); } }
		
		/// <summary>Gets the full path of the Dat object</summary>
		/// <remarks>Defaults to <b>"newfile.dat"</b></remarks>
		public string FilePath { get { return _filePath; } }
		
		/// <summary>Gets the Collection of Groups in the archive</summary>
		public GroupCollection Groups { get; internal set; }
		
		/// <summary>Gets the number of Groups in the archive</summary>
		public short NumberOfGroups { get { return (short)Groups.Count; } }

		/// <summary>Gets the maximum height used for all images</summary>
		public short UsedHeight
		{
			get
			{
				short h = -1;
				foreach (Group g in Groups)
					foreach (Sub s in g.Subs)
						if (s.Height > h) h = s.Height;
				return h;
			}
		}
		/// <summary>Gets the maximum width used for all images</summary>
		public short UsedWidth
		{
			get
			{
				short w = -1;
				foreach (Group g in Groups)
					foreach (Sub s in g.Subs)
						if (s.Width > w) w = s.Width;
				return w;
			}
		}
		#endregion public properties

		void _updateGroupHeaders()
		{
			for (int i = 0; i < Groups.Count; i++)
			{
				// GroupID is always up-to-date
				ArrayFunctions.WriteToArray(Groups[i].NumberOfSubs, Groups[i]._header, 2);
				ArrayFunctions.WriteToArray(Groups[i]._length, Groups[i]._header, 4);
				ArrayFunctions.WriteToArray(Groups[i].Subs.NumberOfColors, Groups[i]._header, 8);
				// Reserved(0) is always up-to-date
				if (i == 0) Groups[i]._dataOffset = 0;
				else Groups[i]._dataOffset = Groups[i - 1]._dataOffset + Groups[i - 1]._length;
			}
		}
		
		/// <summary>Gets sum of Groups.Length values</summary>
		int _length
		{
			get
			{
				int l = 0;
				for (int i = 0; i < Groups.Count; i++) l += Groups[i]._length;
				return l;
			}
		}
		
		int _dataOffset { get { return NumberOfGroups * Group._headerLength; } }
	}
}
