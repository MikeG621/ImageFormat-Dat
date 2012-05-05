/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LucasArts *.DAT Image files
 * Copyright (C) 2009-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in DatFile.cs
 * VERSION: 2.0
 */

/* CHANGE LOG
 * 120405 - split out from DatFile, removed _subs null checks
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
		/// <param name="header">GroupHeader raw data, must have a length of 24</param>
		/// <exception cref="System.ArgumentException"><i>header</i> is not the required length</exception>
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
		/// <exception cref="System.ArgumentException">Not all <i>images</i> are 8bppIndexed</exception>
		/// <exception cref="Idmr.Common.BoundaryException">Not all <i>images</i> meet allowable dimensions</exception>
		/// <remarks><see cref="Sub.SubID"/> starts at <b>0</b> and increments by 1. All <i>images</i> must be 8bppIndexed and are initialized as <b>ImageType.Transparent</b><br/>
		/// To use Blended images, <see cref="Subs"/> must individually have their <see cref="Sub.Type"/> changed and use <see cref="Sub.SetTransparencyMask"/></remarks>
		public Group(short groupID, Bitmap[] images)
		{
			for (int i = 0; i < images.Length; i++)
			{
				if (images[i].PixelFormat != PixelFormat.Format8bppIndexed)
					throw new ArgumentException("All images must be 8bppIndexed", "images[" + i + "]");
				if (images[i].Width > Sub.MaximumWidth || images[i].Height > Sub.MaximumHeight)
					throw new BoundaryException("images[" + i + "]", Sub.MaximumWidth + "x" + Sub.MaximumHeight);
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
		internal int _length
		{
			get
			{
				int l = 0;
				for (int i = 0; i < _subs.Count; i++) l += _subs[i]._length + Sub._subHeaderLength;
				return l;
			}
		}
		/// <summary>***Must be updated at the Dat level***</summary>
		internal int _dataOffset
		{
			get { return BitConverter.ToInt32(_header, 0x14); }
			set { ArrayFunctions.WriteToArray(value, _header, 0x14); }
		}
	}
}
