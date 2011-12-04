/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LA *.DAT Image files
 * Copyright (C) 2009-2011 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * This library is free software; you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3.0 of the License, or (at
 * your option) any later version.
 *
 * This library is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
 * Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this library; if not, write to;
 * Free Software Foundation, Inc.
 * 59 Temple Place, Suite 330
 * Boston, MA 02111-1307 USA
 */

/* CHANGE LOG
 * 110818 - various, Common.Graphics implementation, removed groupID read in Subs, removed GroupHeader
 * 110827 - exceptions reworked
 * 111012 - Blank cons added, added full editing capability for custom DATs
 * 111026 - Encode/Decode added, internals added
 * 111108 - added calls for StringFunctions and ArrayFunctions
 * 111117 - Collections added, 2.0
*/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object to work with *.DAT image archives found in XWA</summary>
	public class DatFile
	{
		string _filePath;
		GroupCollection _groups = null;
		static string _valEx = "Validation error, file is not a LucasArts Act Image file or is corrupted.";
		const long _validationID = 0x5602235657062357;
		const int _groupHeaderLength = 0x18;
		
		/// <summary>Types of image compression</summary>
		/// <remarks>Transparent = 7; 0-8bbp Indexed, Transparent is Color[0]<br>
		/// Blended = 23, 0-16bpp Indexed Alpha compressed<br>
		/// UncompressedBlended = 24; 16bpp Indexed Alpha uncompressed</remarks>
		public enum ImageType : short { Transparent=7, Blended=23, UncompressedBlended };

		#region constructors
		/// <summary>Create a blank Dat archive</summary>
		/// <remarks>FilePath defaults to "newfile.dat"</remarks>
		public DatFile()
		{
			_filePath = "newfile.dat";
		}
		/// <summary>Loads an existing Dat archive</summary>
		/// <param name="file">Full path to the *.dat file</param>
		/// <exception cref="Idmr.Common.LoadFileException">File cannot be loaded, check InnerException</exception>
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
		/// <exception cref="Idmr.Common.SaveFileException">File cannot be saved, check InnerException</exception>
		/// <remarks>Original remains unchanged if applicable</remarks>
		public void Save()
		{
			FileStream fs = null;
			string tempFile = _filePath + ".tmp";
			try
			{
				if (_groups == null) throw new ArgumentException("Groups is undefined");
				_updateGroupHeaders();
				for (int g = 0; g < NumberOfGroups; g++)
					for (int s = 0; s < _groups[g].NumberOfSubs; s++) _groups[g].Subs[s]._headerUpdate();
				if (File.Exists(_filePath)) File.Copy(_filePath, tempFile);	// create backup
				File.Delete(_filePath);
				fs = File.OpenWrite(_filePath);
				BinaryWriter bw = new BinaryWriter(fs);
				// FileHeader
				bw.Write(_validationID);
				bw.Write((short)1);
				bw.Write(NumberOfGroups);
				bw.Write(_groups.NumberOfSubs);
				bw.Write(_length);
				bw.Write(_groups.NumberOfColors);
				fs.Position += 8;	// long 0
				bw.Write(_dataOffset);
				for (int g = 0; g < NumberOfGroups; g++) bw.Write(_groups[g]._header);
				// Groups
				for (int g = 0; g < NumberOfGroups; g++)
				{
					// Subs
					for (int s = 0; s < _groups[g].NumberOfSubs; s++)
					{
						bw.Write(_groups[g].Subs[s]._headers);
						for (int c = 0; c < _groups[g].Subs[s].NumberOfColors; c++)
						{
							bw.Write(_groups[g].Subs[s].Colors[c].R);
							bw.Write(_groups[g].Subs[s].Colors[c].G);
							bw.Write(_groups[g].Subs[s].Colors[c].B);
						}
						bw.Write(_groups[g].Subs[s]._rows);
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
		/// <exception cref="Idmr.Common.SaveFileException">File cannot be saved</exception>
		/// <remarks>Original remains unchanged if applicable</remarks>
		public void Save(string file)
		{
			_filePath = file;
			Save();
		}
		
		/// <summary>Populates the Dat with the raw byte data from file</summary>
		/// <param name="rawData">Entire contents of a *.DAT archive</param>
		/// <exception cref="System.ArgumentException">Validation error</exception>
		public void DecodeFile(byte[] rawData)
		{
			// Dat.FileHeader
			if (BitConverter.ToInt64(rawData, 0) != _validationID) throw new ArgumentException(_valEx, "file");
			if (BitConverter.ToInt16(rawData, 8) != 1) throw new ArgumentException(_valEx, "file");
			short numberOfGroups = BitConverter.ToInt16(rawData, 0xA);
			int offset = 0x22;
			// Dat.GroupHeaders
			_groups = new GroupCollection(numberOfGroups);
			byte[] header = new byte[_groupHeaderLength];
			for (int i = 0; i < numberOfGroups; i++)
			{
				ArrayFunctions.TrimArray(rawData, offset, header);
				_groups[i] = new Group(header);
				offset += _groupHeaderLength;
			}
			// Dat.Groups
			for (int i = 0; i < numberOfGroups; i++)
			{	// Group.Subs
				for (int j = 0; j < _groups[i].NumberOfSubs; j++)
				{
					int length = BitConverter.ToInt32(rawData, offset + 0xE);
					byte[] sub = new byte[length + Sub._subHeaderLength];
					ArrayFunctions.TrimArray(rawData, offset, sub);
					_groups[i].Subs[j] = new Sub(sub);
					offset += sub.Length;
				}
			}
		}

		/// <summary>Returns the image given the encoded Rows data and settings</summary>
		/// <param name="rawData">Encoded Rows data</param>
		/// <param name="width">Image width</param>
		/// <param name="height">Image height</param>
		/// <param name="colors">Defined Color array to be used for the image</param>
		/// <param name="type">Encoding protocol used for <i>rawData</i></param>
		public static Bitmap DecodeImage(byte[] rawData, int width, int height, Color[] colors, ImageType type)
		{
			int offset = 0;
			Bitmap image = new Bitmap(1, 1);	// dummy assignment, image has to be external to switch block
			switch (type)
			{
				case ImageType.Transparent:
					image = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
					BitmapData bdTrans = GraphicsFunctions.GetBitmapData(image);
					byte[] pixelTrans = new byte[bdTrans.Stride * bdTrans.Height];
					// Sub.Rows
					for (int y = 0; y < bdTrans.Height; y++)
					{
						byte numOps = rawData[offset++];	// Row.NumberOfOperations
						for (int n = 0, x = 0, pos = bdTrans.Stride * y; n < numOps; n++)
						{
							byte b = rawData[offset++];	//Row.OpCode
							if (b >= 0x80) x += (b - 0x80);	// transparent length, OpCode.ColorIndex=0
							else for (int k = 0; k < b; k++, x++) pixelTrans[pos + x] = rawData[offset++];	// OpCode.ColorIndex
						}
					}
					GraphicsFunctions.CopyBytesToImage(pixelTrans, bdTrans);
					image.UnlockBits(bdTrans);
					ColorPalette pal = image.Palette;
					for (int c = 0; c < colors.Length; c++) pal.Entries[c] = colors[c];
					for (int c = colors.Length; c < 256; c++) pal.Entries[c] = Color.Blue;
					image.Palette = pal;
					break;
				case ImageType.Blended:
					image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
					BitmapData bdB = GraphicsFunctions.GetBitmapData(image);
					byte[] pixelBlend = new byte[bdB.Stride * bdB.Height];
					// Sub.Rows
					for (int y = 0; y < bdB.Height; y++)
					{
						byte numOps = rawData[offset++];	//Row.NumberOfOperations
						for (int n = 0, x = 0, pos = bdB.Stride * y; n < numOps; n++)
						{
							byte b = rawData[offset++];	// Row.OpCode
							if (b >= 0xC0) x += (b - 0xC0);	// transparent length, OpCode.ColorIndex=0
							else if (b < 0x40)
								for (int k = 0; k < b; k++, x++)
								{	// short read opaque
									Color c = colors[rawData[offset++]];	//OpCode.ColorIndex
									pixelBlend[pos + x * 4] = c.B;
									pixelBlend[pos + x * 4 + 1] = c.G;
									pixelBlend[pos + x * 4 + 2] = c.R;
									pixelBlend[pos + x * 4 + 3] = 255;
								}
							else
								for (int k = 0; k < (b - 0x80); k++, x++)
								{	// alpha read
									pixelBlend[pos + x * 4 + 3] = rawData[offset++];	// Pixel.Alpha
									Color c = colors[rawData[offset++]];	// Pixel.ColorIndex
									pixelBlend[pos + x * 4] = c.B;
									pixelBlend[pos + x * 4 + 1] = c.G;
									pixelBlend[pos + x * 4 + 2] = c.R;
								}
						}
					}
					GraphicsFunctions.CopyBytesToImage(pixelBlend, bdB);
					image.UnlockBits(bdB);
					break;
				case ImageType.UncompressedBlended:
					image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
					BitmapData bdU = GraphicsFunctions.GetBitmapData(image);
					byte[] pixelUncomp = new byte[bdU.Stride * bdU.Height];
					// Sub.Rows
					for (int y = 0; y < bdU.Height; y++)
					{
						for (int x = 0, pos = bdU.Stride * y; x < width; x++)
						{
							Color c = colors[rawData[offset++]];	// Pixel.ColorIndex
							pixelUncomp[pos + x * 4] = c.B;
							pixelUncomp[pos + x * 4 + 1] = c.G;
							pixelUncomp[pos + x * 4 + 2] = c.R;
							pixelUncomp[pos + x * 4 + 3] = rawData[offset++];	// Pixel.Alpha
						}
					}
					GraphicsFunctions.CopyBytesToImage(pixelUncomp, bdU);
					image.UnlockBits(bdU);
					break;
			}
			return image;
		}
		
		/// <summary>Returns the encoded Rows data given the image and settings</summary>
		/// <param name="image">The image to encode</param>
		/// <param name="type">The encoding protocol to use</param>
		/// <param name="colors">The color array to use</param>
		/// <param name="trimmedImage"><i>image</i> with unused color indexes removed</param>
		/// <param name="trimmedColors"><i>colors</i> with unused color indexes removed</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds allowable dimensions</exception>
		/// <remarks>Unused color indexes are removed from both <i>colors</i> and <i>image</i>, the returned array reflects the trimmed parameters</remarks>
		public static byte[] EncodeImage(Bitmap image, ImageType type, Color[] colors, out Bitmap trimmedImage, out Color[] trimmedColors)
		{
			string img = "image";
			if (image.Height > MaximumHeight || image.Width > MaximumWidth)
				throw new BoundaryException(img, MaximumWidth + "x" + MaximumHeight);
			byte[] mask = null;
			if (image.PixelFormat == PixelFormat.Format32bppArgb && type == ImageType.Transparent)
				image = GraphicsFunctions.ConvertTo8bpp(image, colors);
			else if (image.PixelFormat == PixelFormat.Format32bppArgb) // converting from one Blended to another
			{
				mask = _getMaskData(image);
				image = GraphicsFunctions.ConvertTo8bpp(image, colors);
			}
			BitmapData bd = GraphicsFunctions.GetBitmapData(image);
			byte[] pixels = new byte[bd.Stride * bd.Height];	// image is now 8bpp, mask if used is 8bpp
			byte[] rows = new byte[pixels.Length * 2 + 100]; // Assume worst, plus some extra
			GraphicsFunctions.CopyImageToBytes(bd, pixels);
			// get used colors
			bool[] used = new bool[256];
			for (int y = 0; y < bd.Height; y++)
				for (int x = 0, pos = bd.Stride * y; x < bd.Width; x++)
					used[pixels[pos + x]] = true;
			// remove unused palette entries
			int count=1;
			for (int c = 1; c < 256; c++)
			{
				if (!used[c])
				{
					for (int i = count; i < colors.Length - 1; i++) colors[i] = colors[i + 1];
					for (int i = 0; i < pixels.Length; i++) if (pixels[i] > count) pixels[i]--;
				}
				else count++;
			}
			trimmedColors = new Color[count];
			for (int c = 0; c < count; c++) trimmedColors[c] = colors[c];
			// start image
			int offset = 0;
			switch (type)
			{
				case ImageType.Transparent:
					for (int y = 0; y < bd.Height; y++)
					{
						int rowOffset = offset++;
						byte numOps = 0;
						for (int x = 0, pos = bd.Stride * y, len = 1; x < bd.Width; x++)
						{
							try
							{	// throws on last row
								if ((x + len) != bd.Width && len < 0x7F
									&& ((pixels[pos + x] == pixels[pos + x + len] && pixels[pos+x]==0)
									|| (pixels[pos+x] != 0 && pixels[pos+x+len] != 0)))
								{
									len++;
									continue;
								}
							}
							catch { /* do nothing */ }
							numOps++;
							if (pixels[pos + x] == 0) rows[offset++] = Convert.ToByte(len + 0x80);	// transparent
							else
							{
								rows[offset++] = Convert.ToByte(len);
								for (int p = 0; p < len; p++) rows[offset++] = pixels[pos + x + p];
							}
							x += len;
							len = 1;
						}
						rows[rowOffset] = numOps;
					}
					break;
				case ImageType.Blended:
					for (int y = 0; y < bd.Height; y++)
					{
						int rowOffset = offset++;
						byte numOps = 0;
						bool transparent = false;
						Color firstColor = Color.Black;
						for (int x = 0, pos = bd.Stride * y, len = 1; x < bd.Width; )
						{
							if (len == 1) if (pixels[pos + x] == 0 || mask[pos + x] == 0) transparent = true;
							try
							{	// throws on last row
								if ((x + len) != bd.Width && len < 0x3F)
								{
									if ((transparent && pixels[pos + x + len] == 0 || mask[pos + x + len] == 0)
										|| (!transparent && pixels[pos + x + len] != 0 && mask[pos + x + len] != 0
										&& ((mask[pos + x] == 255 && mask[pos + x + len] == 255) || (mask[pos + x] != 255 && mask[pos + x + len] != 255))))
									{
										// ^that ugly thing detects trans lengths, opaque lengths, and alpha lengths. alpha will break on an opaque pixel
										len++;
										continue;
									}
								}
							}
							catch { /* do nothing */ }
							numOps++;
							if (transparent) rows[offset++] = Convert.ToByte(len + 0xC0);	// transparent
							else if (mask[pos + x] == 255)	// opaque
							{
								rows[offset++] = Convert.ToByte(len);
								for (int p = 0; p < len; p++) rows[offset++] = pixels[pos + x + p];
							}
							else // alpha
							{
								rows[offset++] = Convert.ToByte(len + 0x80);
								for (int p = 0; p < len; p++)
								{
									Color c = Color.FromArgb(BitConverter.ToInt32(pixels, pos + (x + p) * 4));
									rows[offset++] = mask[pos + x + p];
									rows[offset++] = pixels[pos + x + p];
								}
							}
							transparent = false;
							x += len;
							len = 1;
						}
						rows[rowOffset] = numOps;
					}
					break;
				case ImageType.UncompressedBlended:
					for (int y = 0; y < bd.Height; y++)
					{
						for (int x = 0, pos = bd.Stride * y; x < bd.Width; x++)
						{
							rows[offset++] = pixels[pos + x];
							rows[offset++] = mask[pos + x];	// Pixel.Alpha
						}
					}
					break;
				
			}
			if (type != ImageType.UncompressedBlended) offset++;	// EndSub
			GraphicsFunctions.CopyBytesToImage(pixels, bd);	// because we messed with colors indexes
			image.UnlockBits(bd);
			if (type != ImageType.Transparent)
			{
				image = new Bitmap(image);
				if (mask != null) image = _applyMaskData(image, mask); // null for Transparent > Blended conversion
			}
			trimmedImage = image;
			byte[] temp = new byte[offset];
			ArrayFunctions.TrimArray(rows, 0, temp);
			return temp;
		}
		#endregion public methods
		
		#region public properties
		/// <summary>Maximum allowable image width</summary>
		public const short MaximumWidth = 640;
		
		/// <summary>Maximum allowable image height</summary>
		public const short MaximumHeight = 480;
		
		/// <summary>Gets the file name of the Dat object</summary>
		public string FileName { get { return StringFunctions.GetFileName(_filePath); } }
		
		/// <summary>Gets the full path of the Dat object</summary>
		public string FilePath { get { return _filePath; } }
		
		/// <summary>Gets or sets the Collection of Groups in the archive</summary>
		public GroupCollection Groups
		{
			get { return _groups; }
			set { _groups = value; }
		}
		
		/// <summary>Gets the number of Groups in the archive</summary>
		public short NumberOfGroups { get { return (short)(_groups != null ? _groups.Count : 0); } }
		#endregion public properties
		
		#region private methods
		static byte[] _getMaskData(Bitmap image)
		{
			BitmapData bdImage = GraphicsFunctions.GetBitmapData(image);
			byte[] pixels = new byte[bdImage.Stride * bdImage.Height];
			GraphicsFunctions.CopyImageToBytes(bdImage, pixels);
			// have to format maskData with same size as 8bpp imageData
			int maskStride = image.Width + (image.Width % 4 == 0 ? 0 : 4 - image.Width % 4);
			byte[] maskData = new byte[maskStride * bdImage.Height];
			for (int y = 0; y < bdImage.Height; y++)
				for(int x = 0, posImage = bdImage.Stride * y, posMask = maskStride * y; x < bdImage.Width; x++)
					maskData[posMask + x] = pixels[posImage + x * 4 + 3];
			image.UnlockBits(bdImage);
			return maskData;
		}
		
		static Bitmap _applyMaskData(Bitmap image, byte[] maskData)
		{
			int maskStride = image.Width + (image.Width % 4 == 0 ? 0 : 4 - image.Width % 4);
			if (maskData.Length != maskStride * image.Height)
				throw new ArgumentException("maskData does not match required length for image", "image, maskData");
			image = new Bitmap(image);
			BitmapData bd = GraphicsFunctions.GetBitmapData(image);
			byte[] pixels = new byte[bd.Stride * bd.Height];
			GraphicsFunctions.CopyImageToBytes(bd, pixels);
			for (int y = 0; y < bd.Height; y++)
				for (int x = 0, posImage = bd.Stride * y, posMask = maskStride * y; x < bd.Width; x++)
					pixels[posImage + x * 4 + 3] = maskData[posMask + x];
			image.UnlockBits(bd);
			return image;
		}
		
		void _updateGroupHeaders()
		{
			for (int i = 0; i < (_groups != null ? _groups.Count : -1); i++)
			{
				// GroupID is always up-to-date
				ArrayFunctions.WriteToArray(_groups[i].NumberOfSubs, _groups[i]._header, 2);
				ArrayFunctions.WriteToArray(_groups[i]._length, _groups[i]._header, 4);
				ArrayFunctions.WriteToArray(_groups[i].Subs.NumberOfColors, _groups[i]._header, 8);
				// Reserved(0) is always up-to-date
				Group g = _groups[i];
				if (i == 0) g._dataOffset = 0;
				else g._dataOffset = _groups[i - 1]._dataOffset + _groups[i - 1]._length;
				_groups[i] = g;	// has to do with being a struct instead of a class
			}
		}
		#endregion private methods
		
		#region private properties
		/// <summary>Gets sum of Groups.Length values</summary>
		int _length
		{
			get
			{
				int l = 0;
				for (int i = 0; i < (_groups != null ? _groups.Count : -1); i++) l += _groups[i]._length;
				return l;
			}
		}
		
		int _dataOffset { get { return (int)(NumberOfGroups * _groupHeaderLength); } }
		#endregion private properties
		
		/// <summary>Container for the Group, acts as a simple wrapper for Subs</summary>
		public struct Group
		{
			SubCollection _subs;
			short _id;
			/// <summary>GroupHeader</summary>
			internal byte[] _header;

			#region constructors
			/// <summary>Create a new Group according to the supplied header information</summary>
			/// <param name="header">GroupHeader raw data</param>
			/// <exception cref="System.ArgumentException"><i>header</i> is not the required length</exception>
			public Group(byte[] header)
			{
				if (header.Length != _groupHeaderLength) throw new ArgumentException("header is not required length", "header");
				_header = header;
				_id = BitConverter.ToInt16(_header, 0);
				_subs = new SubCollection(BitConverter.ToInt16(_header, 2), _id);
			}
			/// <summary>Create a new Group and populate with the given SubCollection</summary>
			/// <param name="subs">Subs to be included in the Group</param>
			public Group(SubCollection subs)
			{
				_header = new byte[_groupHeaderLength];
				_id = subs.GroupID;
				ArrayFunctions.WriteToArray(_id, _header, 0);
				_subs = subs;
			}
			/// <summary>Create an empty Group with the given ID</summary>
			/// <param name="id">Group ID value</param>
			/// <remarks>Subs is set to <i>null</i></remarks>
			public Group(short groupID)
			{
				_header = new byte[_groupHeaderLength];
				_id = groupID;
				ArrayFunctions.WriteToArray(_id, _header, 0);
				_subs = null;
			}
			/// <summary>Creates a new Group and populates it with the given images</summary>
			/// <param name="groupID">Group ID value</param>
			/// <param name="images">Images from which to create the Subs</param>
			/// <exception cref="System.ArgumentException">Not all images are 8bppIndexed</exception>
			/// <exception cref="Idmr.Common.BoundaryException">Not all images meet allowable dimensions</exception>
			/// <remarks>Sub.IDs start at 0 and increment by 1. All images must be 8bppIndexed and are initialized as ImageType.Transparent<br>
			/// To use Blended images, Subs must individually have their Type changed and use SetTransparencyMask()</remarks>
			public Group(short groupID, Bitmap[] images)
			{
				for(int i = 0; i < images.Length; i++)
				{
					if (images[i].PixelFormat != PixelFormat.Format8bppIndexed)
						throw new ArgumentException("All images must be 8bppIndexed", "images[" + i + "]");
					if (images[i].Width > MaximumWidth || images[i].Height > MaximumHeight)
						throw new BoundaryException("images[" + i + "]", MaximumWidth + "x" + MaximumHeight);
				}
				_header = new byte[_groupHeaderLength];
				_id = groupID;
				ArrayFunctions.WriteToArray(_id, _header, 0);
				_subs = new SubCollection(_id, images);
			}
			#endregion constructors

			#region public properties
			/// <summary>Gets the number of Subs within the Group</summary>
			public short NumberOfSubs { get { return (short)(_subs != null ? _subs.Count : -1); } }
			/// <summary>Gets or sets the Collection of Subs within the Group</summary>
			public SubCollection Subs
			{
				get { return _subs; }
				set
				{
					_subs = value;
					_subs.GroupID = _id;
				}
			}
			/// <summary>Gets or Sets the Group Identifier</summary>
			/// <remarks>Updating through GroupCollection.SetID() is preferred</remarks>
			public short ID
			{
				get { return _id; }
				set
				{
					_id = value;
					for (int i = 0; i < (_subs != null ? _subs.Count : -1); i++)
					{
						Sub s = _subs[i];
						s.GroupID = _id;
						_subs[i] = s;
					}
					ArrayFunctions.WriteToArray(_id, _header, 0);
				}
			}
			#endregion public properties
			
			#region private properties
			/// <summary>Sum of Subs.Length values</summary>
			internal int _length
			{
				get
				{
					int l = 0;
					for (int i = 0; i < (_subs != null ? _subs.Count : -1); i++) l += _subs[i]._length + Sub._subHeaderLength;
					return l;
				}
			}
			/// <summary>***Must be updated at the Dat level***</summary>
			internal int _dataOffset
			{
				get { return BitConverter.ToInt32(_header, 0x14); }
				set { ArrayFunctions.WriteToArray(value, _header, 0x14); }
			}
			#endregion private properties
		}

		/// <summary>Container for individual images</summary>
		public struct Sub
		{
			short _groupID;
			short _subID;
			Color[] _colors;
			Bitmap _image;	// if (Type.Transparent) Format8bppIndexed, else Format32bppArgb
            ImageType _type;
			/// <summary>SubHeader + ImageHeader</summary>
			internal byte[] _headers;
			/// <summary>Rows data plus final 0 if required</summary>
			internal byte[] _rows;
			internal const int _subHeaderLength = 0x12;
			internal const int _imageHeaderLength = 0x2C;
			internal const int _imageHeaderReserved = 0x18;

			#region constructors
			/// <summary>Create a new Sub with the provided raw data</summary>
			/// <param name="raw">Complete encoded data of an individual Sub</param>
			/// <exception cref="ArgumentException">Validation error</exception>
			public Sub(byte[] raw)
			{
				_headers = new byte[_subHeaderLength + _imageHeaderLength];
				ArrayFunctions.TrimArray(raw, 0, _headers);
				// Sub.SubHeader
				_type = (ImageType)BitConverter.ToInt16(_headers, 0);
				short width = BitConverter.ToInt16(_headers, 2);
				short height = BitConverter.ToInt16(_headers, 4);
				_groupID = BitConverter.ToInt16(_headers, 0xA);
				_subID = BitConverter.ToInt16(_headers, 0xC);
				int length = BitConverter.ToInt32(_headers, 0xE);
				int offset = _subHeaderLength;
				// Sub.ImageHeader
				if (BitConverter.ToInt32(_headers, offset + 4) != _imageHeaderLength)
					throw new ArgumentException(_valEx, "raw");
				int imageDataOffset = BitConverter.ToInt32(_headers, offset + 8);
				if (BitConverter.ToInt32(_headers, offset + 0x24) != _imageHeaderReserved)
					throw new ArgumentException(_valEx, "raw"); // Reserved
				_colors = new Color[BitConverter.ToInt32(_headers, offset + 0x28)];	// NumberOfColors
				offset += _imageHeaderLength;
				// Sub.Colors[]
				for (int k = 0; k < _colors.Length; k++, offset += 3)
					_colors[k] = Color.FromArgb(raw[offset], raw[offset + 1], raw[offset + 2]);
				// Sub.Rows[]
				_rows = new byte[length - imageDataOffset];
				ArrayFunctions.TrimArray(raw, offset, _rows);
				_image = DecodeImage(_rows, width, height, _colors, _type);
			}
			/// <summary>Create a new sub from the provided image and IDs</summary>
			/// <param name="groupID">Group ID value</param>
			/// <param name="subID">Sub ID value</param>
			/// <param name="image">The image to be used</param>
			/// <exception cref="System.ArgumentException"><i>image</i> is not 8bppIndexed</exception>
			/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds allowable dimensions</exception>
			/// <remarks><i>image</i> must be 8bppIndexed, 640x480 max. Initialized as ImageType.Transparent.<br>
			/// If a Blended type is desired, change Type and SetTransparencyMask()</remarks>
			public Sub(short groupID, short subID, Bitmap image)
			{
				if (image.Height > MaximumHeight || image.Width > MaximumWidth)
					throw new BoundaryException("image", MaximumWidth + "x" + MaximumHeight);
				if (image.PixelFormat != PixelFormat.Format8bppIndexed) throw new ArgumentException("image must be 8bppIndexed", "image");
				_headers = new byte[_subHeaderLength + _imageHeaderLength];
				_groupID = groupID;
				ArrayFunctions.WriteToArray(_groupID, _headers, 0xA);
				_subID = subID;
				ArrayFunctions.WriteToArray(_subID, _headers, 0xC);
				ArrayFunctions.WriteToArray(_imageHeaderLength, _headers, _subHeaderLength + 4);
				ArrayFunctions.WriteToArray(_imageHeaderReserved, _headers, _subHeaderLength + 0x24);
				_type = ImageType.Transparent;
				_rows = EncodeImage(image, _type, (Color[])image.Palette.Entries.Clone(), out _image, out _colors);
				_headerUpdate();
			}
			/// <summary>Create an empty Sub</summary>
			/// <param name="groupID">Group ID value</param>
			/// <param name="subID">Sub ID value</param>
			/// <remarks>Initialized as ImageType.Transparent. Image and Colors set to <i>null</i></remarks>
			public Sub(short groupID, short subID)
			{
				_headers = new byte[_subHeaderLength + _imageHeaderLength];
				_groupID = groupID;
				ArrayFunctions.WriteToArray(_groupID, _headers, 0xA);
				_subID = subID;
				ArrayFunctions.WriteToArray(_subID, _headers, 0xC);
				ArrayFunctions.WriteToArray(_imageHeaderLength, _headers, _subHeaderLength + 4);
				ArrayFunctions.WriteToArray(_imageHeaderReserved, _headers, _subHeaderLength + 0x24);
				_colors = null;
				_image = null;
				_type = ImageType.Transparent;
				_rows = null;
			}
			#endregion constructors

			#region public methods
			/// <summary>Sets the image of the Sub</summary>
			/// <param name="image">The image to be used</param>
			/// <exception cref="ArgumentException">Invalid <i>image</i> value</exception>
			/// <remarks><i>image</i> restricted to 640x480</remarks>
			public void SetImage(Bitmap image)
			{
				_rows = EncodeImage(image, _type, (Color[])_colors.Clone(), out _image, out _colors);
				_headerUpdate();
			}
			/// <summary>Sets the image and transparency of the Sub</summary>
			/// <param name="image">The image to be used</param>
			/// <param name="mask">The transparency mask to be used. Ignored if Type = Transparent</param>
			/// <exception cref="ArgumentException">Invalid <i>image</i> or <i>mask</i> values</exception>
			/// <remarks><i>image</i> restricted to 640x480. <i>mask</i>.Size must match <i>image</i>.Size, must be 8bppIndexed. 0 is transparent, 255 is solid.</remarks>
			public void SetImage(Bitmap image, Bitmap mask)
			{
				SetImage(image);
				SetTransparencyMask(mask);
			}

			/// <summary>Sets the transparency of the Sub for Blended types</summary>
			/// <param name="mask">The transparency mask to be used.</param>
			/// <exception cref="ArgumentException">Invalid <i>mask</i> value</exception>
			/// <remarks><i>mask</i>.Size must match <i>Image</i>.Size, must be 8bppIndexed.<br>
			///	Index 0 is transparent, 255 is solid. Use grayscale image, 0 being black and 255 as white.</remarks>
			public void SetTransparencyMask(Bitmap mask)
			{
				if (_type == ImageType.Transparent) return;
				if (mask.Size != _image.Size) throw new ArgumentException("Incorrect mask.Size", "mask");
				if (mask.PixelFormat != PixelFormat.Format8bppIndexed) throw new ArgumentException("Mask must be 8bppIndexed", "mask");
				BitmapData bdMask = GraphicsFunctions.GetBitmapData(mask);
				byte[] maskData = new byte[bdMask.Stride * bdMask.Height];	// 8 bit
				GraphicsFunctions.CopyImageToBytes(bdMask, maskData);
				mask.UnlockBits(bdMask);	// don't need it anymore
				_image = _applyMaskData(_image, maskData);
			}
			#endregion public methods
			
			#region public properties
			/// <summary>Gets or sets the format of the raw byte data</summary>
			/// <remarks>Transparency data is lost when going to ImageType.Transparent</remarks>
            public ImageType Type
			{
				get { return _type; }
				set
				{
					if (_image == null) throw new ArgumentException("Cannot change type without a defined Image");
					_rows = EncodeImage(_image, value, (Color[])_colors.Clone(), out _image, out _colors);
					_type = value;
					_headerUpdate();
				}
			}

			/// <summary>Gets the width of the image</summary>
			public short Width { get { return (short)(_image != null ? _image.Width : 0); } }

			/// <summary>Gets the height of the image</summary>
			public short Height { get { return (short)(_image != null ? _image.Height : 0); } }

			/// <summary>Gets or Sets the ID of the parent Group</summary>
			public short GroupID
			{
				get { return _groupID; }
				set
				{
					_groupID = value;
					ArrayFunctions.WriteToArray(_groupID, _headers, 0xA);
				}
			}

			/// <summary>Gets or Sets the ID of the Sub</summary>
			public short SubID
			{
				get { return _subID; }
				set
				{
					_subID = value;
					ArrayFunctions.WriteToArray(_subID, _headers, 0xC);
				}
			}

			/// <summary>Gets the number of Colors defined in the Sub</summary>
			public int NumberOfColors { get { return (_colors != null ? _colors.Length : 0); } }

			/// <summary>Collection of Colors defined by the Sub</summary>
			public Color[] Colors
			{
				get { return _colors; }
				set
				{
					if (_image == null) throw new ArgumentException("Cannot set colors without a defined Image");
					if (value.Length > 256) throw new ArgumentException("256 colors max");
					if (_image.PixelFormat == PixelFormat.Format8bppIndexed)
					{
						ColorPalette pal = _image.Palette;
						for (int i = 0; i < value.Length; i++) pal.Entries[i] = value[i];
						_image.Palette = pal;
					}
					else
					{
						byte[] maskData = _getMaskData(_image);
						Bitmap temp8bpp = GraphicsFunctions.ConvertTo8bpp(_image, _colors);	//force back to 8bpp
						ColorPalette pal = temp8bpp.Palette;
						for (int i = 0; i < value.Length; i++) pal.Entries[i] = value[i];
						temp8bpp.Palette = pal;	// apply new colors
						_image = _applyMaskData(temp8bpp, maskData);
					}
					_colors = value;
					_headerUpdate();
				}
			}

			/// <summary>Gets the image of the Sub</summary>
			public Bitmap Image { get { return _image; } }
			#endregion public properties
			
			#region private methods
			internal void _headerUpdate()
			{
				if (_image == null) throw new ArgumentException("Image is undefined");
				byte[] width = BitConverter.GetBytes((short)_image.Width);
				byte[] height = BitConverter.GetBytes((short)_image.Height);
				byte[] length = BitConverter.GetBytes(_length);
				byte[] type = BitConverter.GetBytes((short)_type);
				ArrayFunctions.WriteToArray(type, _headers, 0);
				ArrayFunctions.WriteToArray(width, _headers, 2);
				ArrayFunctions.WriteToArray(height, _headers, 4);
				// int 0
				// groupID up-to-date
				// subID up-to-date
				ArrayFunctions.WriteToArray(length, _headers, 0xE);
				ArrayFunctions.WriteToArray(length, _headers, _subHeaderLength);
				// imageHeaderLength
				ArrayFunctions.WriteToArray(_imageDataOffset, _headers, _subHeaderLength + 8);
				ArrayFunctions.WriteToArray(length, _headers, _subHeaderLength + 0xC);
				ArrayFunctions.WriteToArray(width, _headers, _subHeaderLength + 0x10);
				// short 0
				ArrayFunctions.WriteToArray(height, _headers, _subHeaderLength + 0x14);
				// short 0
				// long 0
				ArrayFunctions.WriteToArray(type, _headers, _subHeaderLength + 0x20);
				// short 0
				// imageHeaderReserved
				ArrayFunctions.WriteToArray(_colors.Length, _headers, _subHeaderLength + 0x28);
			}
			#endregion private methods
			
			#region private properties
			/// <summary>SubHeader.Length = ImageHeader.ImageDataOffset + _rows.Length</summary>
			internal int _length { get { return _imageDataOffset + (_rows != null ? _rows.Length : 0); } }
			
			/// <summary>ImageHeader.ImageDataOffset = ImageHeaderLength + NumberOfColors * 3</summary>
			internal int _imageDataOffset { get { return _imageHeaderLength + (_colors != null ? _colors.Length * 3 : 0); } }
			#endregion private properties
		}
	}
}
