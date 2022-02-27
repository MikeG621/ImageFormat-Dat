/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LucasArts *.DAT Image files
 * Copyright (C) 2009-2022 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in DatFile.cs
 * VERSION: 2.2
 */

/* CHANGE LOG
 * v2.2, 190922
 * [DEL] max image size checks
 * v2.1, 141214
 * [UPD] switch to MPL
 * v2.0, 120505
 * [DEL] _subs null checks
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using Idmr.Common;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object for individual Groups, acts as a simple wrapper for <see cref="Subs"/></summary>
	public class Group
	{
		SubCollection _subs;
		short _id;
		internal byte[] _header;
		internal const int _headerLength = 0x18;

		#region constructors
		/// <summary>Creates a new Group according to the supplied header information</summary>
		/// <param name="header">GroupHeader raw data, must have a length of <b>24</b></param>
		/// <exception cref="ArgumentException"><paramref name="header"/> is not the required length</exception>
		public Group(byte[] header)
		{
			if (header.Length != _headerLength) throw new ArgumentException("header must have a length of " + _headerLength, "header");
			_header = header;
			_id = BitConverter.ToInt16(_header, 0);
			_subs = new SubCollection(BitConverter.ToInt16(_header, 2), _id);
		}
		/// <summary>Creates a new Group and populate with the given SubCollection</summary>
		/// <param name="subs">Subs to be included in the Group</param>
		public Group(SubCollection subs)
		{
			_header = new byte[_headerLength];
			_id = subs.GroupID;
			ArrayFunctions.WriteToArray(_id, _header, 0);
			_subs = subs;
		}
		/// <summary>Creates an empty Group with the given ID</summary>
		/// <param name="groupID">Group ID value</param>
		/// <remarks><see cref="Subs"/> is initialized as empty</remarks>
		public Group(short groupID)
		{
			_header = new byte[_headerLength];
			_id = groupID;
			ArrayFunctions.WriteToArray(_id, _header, 0);
			_subs = new SubCollection(_id);
		}
		/// <summary>Creates a new Group and populates it with the given images</summary>
		/// <param name="groupID">Group ID value</param>
		/// <param name="images">Images from which to create the Subs</param>
		/// <exception cref="ArgumentException">Not all <paramref name="images"/> are 8bppIndexed</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="images"/> has more than than 256 elements.</exception>
		/// <remarks><see cref="Sub.SubID"/> starts at <b>0</b> and increments by 1. All <paramref name="images"/> must be 8bppIndexed and are initialized as <see cref="Sub.ImageType.Transparent"/><br/>
		/// To use Blended images, <see cref="Subs"/> must individually have their <see cref="Sub.Type"/> changed and use <see cref="Sub.SetTransparencyMask"/></remarks>
		public Group(short groupID, Bitmap[] images)
		{
			for (int i = 0; i < images.Length; i++)
			{
				if (images[i].PixelFormat != PixelFormat.Format8bppIndexed)
					throw new ArgumentException("All images must be 8bppIndexed", "images[" + i + "]");
			}
			_header = new byte[_headerLength];
			_id = groupID;
			ArrayFunctions.WriteToArray(_id, _header, 0);
			_subs = new SubCollection(_id, images);
		}
		#endregion constructors

		#region public properties
		/// <summary>Gets the number of Subs within the Group</summary>
		public short NumberOfSubs { get { return (short)_subs.Count; } }

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

		/// <summary>Gets or sets the Group Identifier</summary>
		/// <remarks>Updating through <see cref="GroupCollection.SetGroupID"/> is preferred. This cannot check for duplicate ID values.</remarks>
		public short ID
		{
			get { return _id; }
			set
			{
				_id = value;
				for (int i = 0; i < _subs.Count; i++) _subs[i].GroupID = _id;
				ArrayFunctions.WriteToArray(_id, _header, 0);
			}
		}
		#endregion public properties

		/// <summary>Sum of Subs.Length values</summary>
		internal int length
		{
			get
			{
				int l = 0;
				for (int i = 0; i < _subs.Count; i++) l += _subs[i]._length + Sub._subHeaderLength;
				return l;
			}
		}
		/// <summary>***Must be updated at the Dat level***</summary>
		internal int dataOffset
		{
			get { return BitConverter.ToInt32(_header, 0x14); }
			set { ArrayFunctions.WriteToArray(value, _header, 0x14); }
		}
	}
}
