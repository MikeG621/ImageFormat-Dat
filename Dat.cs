/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LA *.DAT Image files
 * Copyright (C) 2010 Michael Gaisser (jaggedfel621@gmail.com)
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

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Idmr.ImageFormat
{
	/// <summary>Object to work with *.DAT image archives found in XWA</summary>
	public class Dat
	{
		private string _filePath;
		private short _numberOfSubs;
		private int _length;
		private int _numberOfColors;
		private int _firstDataOffset;
		private GroupHeader[] _groupHeaders;
		private Group[] _groups;

		/// <summary>Loads an existing Dat archive</summary>
		/// <param name="file">Full path to the *.dat file</param>
		/// <exception cref="ArgumentException">File validation errors</exception>
		/// <exception cref="UnauthorizedAccessException"></exception>
		/// <exception cref="FileNotFoundException"></exception>
		public Dat(string file)
		{
			_filePath = file;
			if (!File.Exists(_filePath)) throw new FileNotFoundException(_filePath + " not found.");
			if (!_filePath.ToUpper().EndsWith(".DAT")) throw new ArgumentException("Invalid file extension, must be .DAT");
			FileStream fs;
			try { fs = File.OpenRead(_filePath); }
			catch (UnauthorizedAccessException) { throw; }
			BinaryReader br = new BinaryReader(fs);
			// FileHeader
			if (br.ReadInt64() != 0x5602235657062357) throw new ArgumentException("File is not a LucasArts Dat Image Archive");
			if (br.ReadInt16() != 1) throw new ArgumentException("File validation error");
			short numberOfGroups = br.ReadInt16();
			_numberOfSubs = br.ReadInt16();
			_length = br.ReadInt32();
			_numberOfColors = br.ReadInt32();
			fs.Position += 8;
			// long Reserved (0)
			_firstDataOffset = br.ReadInt32();
			// Groups
			_groupHeaders = new GroupHeader[numberOfGroups];
			for (int i=0;i<numberOfGroups;i++)
			{
				short id = br.ReadInt16(), numberOfSubs = br.ReadInt16();
				int length = br.ReadInt32(), numberOfColors = br.ReadInt32();
				fs.Position += 8;
				int dataOffset = br.ReadInt32();
				_groupHeaders[i] = new GroupHeader(id, numberOfSubs, length, numberOfColors, dataOffset);
			}
			// Images
			_groups = new Group[numberOfGroups];
			for (int i=0;i<numberOfGroups;i++)
			{
				_groups[i] = new Group(_groupHeaders[i].NumberOfSubs);
				for (int j=0;j<_groups[i].NumberOfSubs;j++)
				{
					ImageType type = (ImageType)br.ReadInt16();
					short width = br.ReadInt16(), height = br.ReadInt16();
					fs.Position += 4;
					short groupID = br.ReadInt16(), subID = br.ReadInt16();
					int length = br.ReadInt32();
					long p = fs.Position;
					fs.Position += 0x28;
					// Palette
					Color[] colors = new Color[br.ReadInt32()];
					for (int k=0;k<colors.Length;k++) { colors[k] = Color.FromArgb(br.ReadByte(), br.ReadByte(), br.ReadByte()); }
					// Image
					Bitmap image = new Bitmap(1,1);	// dummy assignment
					BitmapData bd;
					byte[] pixelData;
					switch (type)
					{
						case ImageType.Transparent:
							image = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
							bd = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
							pixelData = new byte[bd.Stride*bd.Height];
							// Rows
							for (int y=0;y<bd.Height;y++)
							{
								byte numOps = br.ReadByte();
								for (int n=0, x=0, pos=bd.Stride*y;n<numOps;n++)
								{
									byte b = br.ReadByte();
									if (b >= 0x80) x += (b - 0x80);	// transparent length
									else for (int k=0;k<b;k++, x++) pixelData[pos+x] = br.ReadByte();
								}
							}
							fs.Position++;
							CopyBytesToImage(pixelData, bd.Scan0);
							image.UnlockBits(bd);
							ColorPalette pal = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
							for (int c=0;c<colors.Length;c++) pal.Entries[c] = colors[c];
							for (int c=colors.Length;c<256;c++) pal.Entries[c] = Color.Blue;
							image.Palette = pal;
							break;
						case ImageType.Blended:
							image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
							bd = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
							pixelData = new byte[bd.Stride*bd.Height];
							// Rows
							for (int y=0;y<bd.Height;y++)
							{
								byte numOps = br.ReadByte();
								for (int n=0, x=0, pos=bd.Stride*y;n<numOps;n++)
								{
									byte b = br.ReadByte();
									if ((b & 0xC0) == 0xC0) x += (b - 0xC0);	// transparent length
									else if ((b & 0x80) != 0x80)
										for (int k=0;k<b;k++, x++)
										{
											// short read
											Color c = colors[br.ReadByte()];
											pixelData[pos+x*4] = c.B;
											pixelData[pos+x*4+1] = c.G;
											pixelData[pos+x*4+2] = c.R;
											pixelData[pos+x*4+3] = 255;
										}
									else
										for (int k=0;k<(b-0x80);k++, x++)
										{
											// alpha read
											pixelData[pos+x*4+3] = br.ReadByte();	// Alpha
											Color c = colors[br.ReadByte()];
											pixelData[pos+x*4] = c.B;
											pixelData[pos+x*4+1] = c.G;
											pixelData[pos+x*4+2] = c.R;
										}
								}
							}
							fs.Position++;
							CopyBytesToImage(pixelData, bd.Scan0);
							image.UnlockBits(bd);
							break;
						case ImageType.UncompressedBlended:
							image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
							bd = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
							pixelData = new byte[bd.Stride*bd.Height];
							// Rows
							for (int y=0;y<bd.Height;y++)
							{
								for (int x=0, pos=bd.Stride*y;x<width;x++)
								{
									Color c = colors[br.ReadByte()];
									pixelData[pos+x*4] = c.B;
									pixelData[pos+x*4+1] = c.G;
									pixelData[pos+x*4+2] = c.R;
									pixelData[pos+x*4+3] = br.ReadByte();
								}
							}
							CopyBytesToImage(pixelData, bd.Scan0);
							image.UnlockBits(bd);
							break;
					}
					_groups[i].Subs[j] = new Sub(type, groupID, subID, length, colors, image);
				}
			}
			fs.Close();
		}

		public string FilePath { get { return _filePath; } }
		public int FirstDataOffset { get { return _firstDataOffset; } }
		public GroupHeader[] GroupHeaders { get { return _groupHeaders; } }
		public Group[] Groups { get { return _groups; } }
		public int Length { get { return _length; } }
		public int NumberOfColors { get { return _numberOfColors; } }
		public short NumberOfGroups { get { return (short)_groups.Length; } }
		public short NumberOfSubs { get { return _numberOfSubs; } }

		public struct GroupHeader
		{
			private short _id;
			private short _numberOfSubs;
			private int _length;
			private int _numberOfColors;
			private int _dataOffset;

			public GroupHeader(short id, short numberOfSubs, int length, int numberOfColors, int dataOffset)
			{
				_id = id;
				_numberOfSubs = numberOfSubs;
				_length = length;
				_numberOfColors = numberOfColors;
				_dataOffset = dataOffset;
			}

			public short ID { get { return _id; } }
			public short NumberOfSubs{ get { return _numberOfSubs; } }
			public int Length { get { return _length; } }
			public int NumberOfColors { get { return _numberOfColors; } }
			// long 0
			public int DataOffset { get { return _dataOffset; } }
		}

		public struct Group
		{
			private Sub[] _subs;

			public Group(int numberOfSubs)
			{
				_subs = new Sub[numberOfSubs];
			}

			public short NumberOfSubs { get { return (short)_subs.Length; } }
			public Sub[] Subs { get { return _subs; } }
		}

		public struct Sub
		{
			private short _groupID;
			private short _subID;
			private int _length;
			private Color[] _colors;
			private Bitmap _image;

			public Sub(ImageType type, short groupID, short subID, int length, Color[] colors, Bitmap image)
			{
				Type = type;
				_groupID = groupID;
				_subID = subID;
				_length = length;
				_colors = colors;
				_image = image;
			}

			public ImageType Type;	// (short)
			public short Width { get { return (short)_image.Width; } }
			public short Height { get { return (short)_image.Height; } }
			// int 0
			public short GroupID { get { return _groupID; } }
			public short SubID { get { return _subID; } }
			public int Length { get { return _length; } }	// ImageHeader + Colors[] + Rows[]
			// -- end SubHeader --
			// int Length again
			public const int ImageHeaderLength = 0x2C;
			public int ImageDataOffset { get { return ImageHeaderLength + _colors.Length*3; } }
			// int Length again
			// int Width
			// int Height
			// long 0
			// ImageType Type again(int)
			// int 0x18
			public int NumberOfColors { get { return _colors.Length; } }
			// -- end ImageHeader --
			public Color[] Colors
			{
				get { return _colors; }
				set { _colors = value; }
			}
			public Bitmap Image
			{
				get { return _image; }
				set { _image = value; }
			}
		}

		public enum ImageType { Transparent=7, Blended=23, UncompressedBlended };

		private void CopyBytesToImage(byte[] source, IntPtr destination)
		{
			System.Runtime.InteropServices.Marshal.Copy(source, 0, destination, source.Length);
		}
		private void CopyImageToBytes(IntPtr source, byte[] destination)
		{
			System.Runtime.InteropServices.Marshal.Copy(source, destination, 0, destination.Length);
		}
	}
}
