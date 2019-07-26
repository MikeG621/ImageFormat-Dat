/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LucasArts *.DAT Image files
 * Copyright (C) 2009-2014 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v2.0 or later
 * 
 * Full notice in DatFile.cs
 * VERSION: 2.1+
 */

/* CHANGE LOG
 * [NEW] Format 25, 32bpp ARGB
 * [DEL] max image limits
 * v2.1, 141214
 * [UPD] switch to MPL
 * v2.0.1, 121024
 * [UPD] SetImage uses image.Palette if image is 8bppIndexed
 * v2.0, 120505
 * [NEW] more null checks
 * [UPD] if _colors is null, SetImage uses default 256-color palette
 * [UPD] allowed Colors.set without defined _image
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using Idmr.Common;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object for individual images</summary>
	public class Sub
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

		/// <summary>Types of image compression</summary>
		public enum ImageType : short
		{
			/// <summary>0-8bpp Indexed, Transparent is <see cref="Colors"/>[<b>0</b>]</summary>
			Transparent = 7,
			/// <summary>0-16bpp Indexed Alpha compressed</summary>
			Blended = 23,
			/// <summary>16bpp Indexed Alpha uncompressed</summary>
			UncompressedBlended,
			/// <summary>32bpp ARGB</summary>
			Full32bppARGB
		};

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
				throw new ArgumentException(DatFile._valEx, "raw");
			int imageDataOffset = BitConverter.ToInt32(_headers, offset + 8);
			if (BitConverter.ToInt32(_headers, offset + 0x24) != _imageHeaderReserved)
				throw new ArgumentException(DatFile._valEx, "raw"); // Reserved
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
		/// <exception cref="ArgumentException"><paramref name="image"/> is not <see cref="PixelFormat.Format8bppIndexed"/></exception>
		/// <remarks><paramref name="image"/> must be <see cref="PixelFormat.Format8bppIndexed"/> with <b>640x480</b> maximum dimensions. Initialized as <see cref="ImageType.Transparent"/>.<br/>
		/// If a Blended type is desired, change <see cref="Type"/> and use <see cref="SetTransparencyMask"/></remarks>
		public Sub(short groupID, short subID, Bitmap image)
		{
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
		/// <remarks>Initialized as <see cref="ImageType.Transparent"/>. <see cref="Image"/> and <see cref="Colors"/> set to <b>null</b></remarks>
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
		/// <summary>Returns the image given the encoded Rows data and settings</summary>
		/// <param name="rawData">Encoded Rows data</param>
		/// <param name="width">Image width</param>
		/// <param name="height">Image height</param>
		/// <param name="colors">Defined Color array to be used for the image</param>
		/// <param name="type">Encoding protocol used for <i>rawData</i></param>
		/// <returns>If <paramref name="type"/> is <see cref="ImageType.Transparent"/>, returns a <see cref="PixelFormat.Format8bppIndexed"/> image.<br/>
		/// Otherwise the returned image will be <see cref="PixelFormat.Format32bppArgb"/></returns>
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
				case ImageType.Full32bppARGB:
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

		/// <summary>Encodes the image for writing to disk</summary>
		/// <param name="image">The image to encode</param>
		/// <param name="type">The encoding protocol to use</param>
		/// <param name="colors">The color array to use</param>
		/// <param name="trimmedImage"><paramref name="image"/> with unused color indexes removed</param>
		/// <param name="trimmedColors"><paramref name="colors"/> with unused color indexes removed</param>
		/// <remarks>Unused color indexes are removed from both <paramref name="colors"/> and <paramref name="image"/>, the returned array reflects the trimmed parameters</remarks>
		/// <returns>Encoded byte data of <paramref name="trimmedImage"/></returns>
		public static byte[] EncodeImage(Bitmap image, ImageType type, Color[] colors, out Bitmap trimmedImage, out Color[] trimmedColors)
		{
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
			int count = 1;
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
									&& ((pixels[pos + x] == pixels[pos + x + len] && pixels[pos + x] == 0)
									|| (pixels[pos + x] != 0 && pixels[pos + x + len] != 0)))
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
				case ImageType.Full32bppARGB:
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
			if (type != ImageType.UncompressedBlended && type != ImageType.Full32bppARGB) offset++;	// EndSub
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

		/// <summary>Sets the image of the Sub</summary>
		/// <param name="image">The image to be used</param>
		/// <exception cref="BoundaryException"><paramref name="image"/> exceeds allowable dimensions</exception>
		/// <remarks><paramref name="image"/> restricted to 640x480, unused color indexes will be removed.<br/>
		/// If <see cref="Colors"/> is undefined, is initialized to the default 256 color palette. Unused indexes will be removed<br/>
		/// If <paramref name="image"/> is <see cref="PixelFormat.Format8bppIndexed"/>, Colors will initialize to the image's palette.</remarks>
		public void SetImage(Bitmap image)
		{
			if (image.PixelFormat == PixelFormat.Format8bppIndexed) _colors = image.Palette.Entries;
			if (_colors == null) _colors = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette.Entries;
			_rows = EncodeImage(image, _type, (Color[])_colors.Clone(), out _image, out _colors);
			_headerUpdate();
		}
		/// <summary>Sets the image and transparency of the Sub</summary>
		/// <param name="image">The image to be used</param>
		/// <param name="mask">The transparency mask to be used. Ignored if <see cref="Type"/> is <see cref="ImageType.Transparent"/></param>
		/// <exception cref="ArgumentException">Invalid <paramref name="mask"/> PixelFormat</exception>
		/// <exception cref="BoundaryException"><paramref name="image"/> exceeds allowable dimensions<br/><b>-or-</b><br/><paramref name="mask"/> is not the required size</exception>
		/// <remarks><paramref name="image"/>.Size restricted to <b>640x480</b>, unused color indexes will be removed.<br/>
		/// <paramref name="mask"/>.Size must match <paramref name="image"/>.Size, must be <see cref="PixelFormat.Format8bppIndexed"/>. Pixel indexes are <b>0</b> for transparent to <b>255</b> for solid.<br/>
		/// If <see cref="Colors"/> is undefined, is initialized to the default 256 color palette. Unused indexes will be removed<br/>
		/// If <paramref name="image"/> is <see cref="PixelFormat.Format8bppIndexed"/>, Colors will initialize to the image's palette.</remarks>
		public void SetImage(Bitmap image, Bitmap mask)
		{
			SetImage(image);
			SetTransparencyMask(mask);
		}

		/// <summary>Sets the transparency of the Sub for Blended types</summary>
		/// <param name="mask">The transparency mask to be used.</param>
		/// <exception cref="ArgumentException">Invalid <paramref name="mask"/> <see cref="PixelFormat"/></exception>
		/// <exception cref="InvalidOperationException"><see cref="Image"/> must be defined before the mask can be set</exception>
		/// <exception cref="BoundaryException"><paramref name="mask"/> is not the required size</exception>
		/// <remarks>No effect if <see cref="Type"/> is <see cref="ImageType.Transparent"/>.<br/>
		/// <paramref name="mask"/>.Size must match <see cref="Image"/>.Size, must be <see cref="PixelFormat.Format8bppIndexed"/>.<br/>
		///	Pixel indexes are <b>0</b> for transparent to <b>255</b> for solid. This is best done with a grayscale image, 0 being black and 255 as white.</remarks>
		public void SetTransparencyMask(Bitmap mask)
		{
			if (_type == ImageType.Transparent) return;
			if (mask.Size != _image.Size) throw new BoundaryException("Mask is not the required size (" + _image.Width + "x" + _image.Height + ")");
			if (mask.PixelFormat != PixelFormat.Format8bppIndexed) throw new ArgumentException("Mask must be 8bppIndexed", "mask");
			if (_image == null) throw new InvalidOperationException("Image must be defined before the mask");
			BitmapData bdMask = GraphicsFunctions.GetBitmapData(mask);
			byte[] maskData = new byte[bdMask.Stride * bdMask.Height];	// 8 bit
			GraphicsFunctions.CopyImageToBytes(bdMask, maskData);
			mask.UnlockBits(bdMask);	// don't need it anymore
			_image = _applyMaskData(_image, maskData);
		}
		#endregion public methods

		#region public properties
		/// <summary>Gets or sets the format of the raw byte data</summary>
		/// <remarks>Transparency data is lost when going to <see cref="ImageType.Transparent"/>.<br/>
		/// If <see cref="Image"/> is defined, is converted to the new <see cref="ImageType"/></remarks>
		public ImageType Type
		{
			get { return _type; }
			set
			{
				if (_image != null) _rows = EncodeImage(_image, value, (Color[])_colors.Clone(), out _image, out _colors);
				_type = value;
				_headerUpdate();
			}
		}

		/// <summary>Gets the width of the image</summary>
		/// <remarks>If <see cref="Image"/> is undefined, value is <b>0</b></remarks>
		public short Width { get { return (short)(_image != null ? _image.Width : 0); } }

		/// <summary>Gets the height of the image</summary>
		/// <remarks>If <see cref="Image"/> is undefined, value is <b>0</b></remarks>
		public short Height { get { return (short)(_image != null ? _image.Height : 0); } }

		/// <summary>Gets or sets the ID of the parent <see cref="Group"/></summary>
		public short GroupID
		{
			get { return _groupID; }
			set
			{
				_groupID = value;
				ArrayFunctions.WriteToArray(_groupID, _headers, 0xA);
			}
		}

		/// <summary>Gets or sets the image ID</summary>
		public short SubID
		{
			get { return _subID; }
			set
			{
				_subID = value;
				ArrayFunctions.WriteToArray(_subID, _headers, 0xC);
			}
		}

		/// <summary>Gets the defined number of <see cref="Colors"/></summary>
		public int NumberOfColors { get { return (_colors != null ? _colors.Length : 0); } }

		/// <summary>Gets or sets the defined colors</summary>
		/// <remarks><i>value</i> is limited to <b>256</b> colors.<br/>
		/// If <see cref="Image"/> is defined, is updated with the new colors.</remarks>
		/// <exception cref="ArgumentOutOfRangeException"><i>value</i> exceeds 256 colors</exception>
		public Color[] Colors
		{
			get { return _colors; }
			set
			{
				if (value.Length > 256) throw new ArgumentOutOfRangeException("256 colors max");
				if (_image != null)
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

		static byte[] _getMaskData(Bitmap image)
		{
			BitmapData bdImage = GraphicsFunctions.GetBitmapData(image);
			byte[] pixels = new byte[bdImage.Stride * bdImage.Height];
			GraphicsFunctions.CopyImageToBytes(bdImage, pixels);
			// have to format maskData with same size as 8bpp imageData
			int maskStride = image.Width + (image.Width % 4 == 0 ? 0 : 4 - image.Width % 4);
			byte[] maskData = new byte[maskStride * bdImage.Height];
			for (int y = 0; y < bdImage.Height; y++)
				for (int x = 0, posImage = bdImage.Stride * y, posMask = maskStride * y; x < bdImage.Width; x++)
					maskData[posMask + x] = pixels[posImage + x * 4 + 3];
			image.UnlockBits(bdImage);
			return maskData;
		}
		#endregion private methods

		/// <summary>SubHeader.Length = ImageHeader.ImageDataOffset + _rows.Length</summary>
		internal int _length { get { return _imageDataOffset + (_rows != null ? _rows.Length : 0); } }

		/// <summary>ImageHeader.ImageDataOffset = ImageHeaderLength + NumberOfColors * 3</summary>
		internal int _imageDataOffset { get { return _imageHeaderLength + (_colors != null ? _colors.Length * 3 : 0); } }
	}
}
