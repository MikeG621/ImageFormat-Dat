/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LucasArts *.DAT Image files
 * Copyright (C) 2009-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in DatFile.cs
 * VERSION: 2.0.1
 */

/* CHANGE LOG
 * v2.0, 120505
 * [NEW] AutoSort
 * [DEL] _items null checks
 * [UPD] inherits ResizableCollection
 * [UPD] _sort to Sort and public
 */

using System;
using System.Collections.Generic;
using Idmr.Common;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object to maintain Dat archive <see cref="Sub">Subs</see></summary>
	/// <remarks><see cref="Idmr.Common.ResizableCollection{T}.ItemLimit"/> is set to <b>200</b></remarks>
	public class SubCollection : ResizableCollection<Sub>
	{
		short _groupID = 0;
		
		#region constructors
		/// <summary>Creates an empty Collection</summary>
		/// <param name="groupID">ID value of the parent <see cref="Group"/></param>
		/// <remarks><see cref="AutoSort"/> is set to <b>true</b>.</remarks>
		public SubCollection(short groupID)
		{
			_groupID = groupID;
			AutoSort = true;
			_itemLimit = 200;
			_items = new List<Sub>(_itemLimit);
		}
		/// <summary>Creates a Collection with multiple initial <see cref="Sub">Subs</see></summary>
		/// <param name="quantity">Number of Subs to start with</param>
		/// <param name="groupID">ID value of the parent <see cref="Group"/></param>
		/// <exception cref="System.ArgumentOutOfRangeException"><i>quantity</i> is not positive and less than <see cref="ResizableCollection{T}.ItemLimit"/></exception>
		/// <remarks>Individual Subs are created with <i>groupID</i> and <see cref="Sub.SubID">SubIDs</see> starting from <b>0</b>.<br/>
		/// Sub colors and images remain uninitialized.<br/>
		/// <see cref="AutoSort"/> is set to <b>true</b></remarks>
		public SubCollection(int quantity, short groupID)
		{
			_itemLimit = 200;
			if (quantity < 1 || quantity > _itemLimit) throw new ArgumentOutOfRangeException("quantity must be positive and less than " + _itemLimit);
			_items = new List<Sub>(_itemLimit);
			_groupID = groupID;
			for (short i = 0; i < quantity; i++) Add(new Sub(_groupID, i));
			AutoSort = true;
		}
		/// <summary>Creates a Collection and populates it with the provided Subs</summary>
		/// <param name="subs">Initial Subs</param>
		/// <remarks><see cref="Sub.GroupID"/> is defined by first Sub in the array</remarks>
		public SubCollection(Sub[] subs)
		{
			_itemLimit = 200;
			_items = new List<Sub>(_itemLimit);
			for (int i = 0; i < subs.Length; i++) Add(subs[i]);
			GroupID = subs[0].GroupID;
		}
		/// <summary>Creates a Collection and populates it with Subs created from the provided images</summary>
		/// <param name="groupID">ID value of the parent <see cref="Group"/></param>
		/// <param name="images">Images from which to create the Subs</param>
		/// <remarks><i>images</i> must all be <see cref="System.Drawing.Imaging.PixelFormat.Format8bppIndexed"/> with <b>640x480</b> maximum dimensions. Initialized as <see cref="Sub.ImageType.Transparent"/>. If a Blended type is desired, change <see cref="Sub.Type"/> and use <see cref="Sub.SetTransparencyMask"/>.<br/>
		/// <see cref="AutoSort"/> is set to <b>true</b>.</remarks>
		public SubCollection(short groupID, System.Drawing.Bitmap[] images)
		{
			_itemLimit = 200;
			_items = new List<Sub>(_itemLimit);
			_groupID = groupID;
			for(short i = 0; i < images.Length; i++) Add(new Sub(groupID, i, images[i]));
			AutoSort = true;
		}
		#endregion constructors
		
		#region public methods
		/// <summary>Adds an empty Sub</summary>
		/// <param name="subID">ID value for the new Sub</param>
		/// <exception cref="System.ArgumentException"><i>subID</i> is already in use</exception>
		/// <returns>Index of the Sub within the Collection</returns>
		/// <remarks>Sub is added and then if <see cref="AutoSort"/> is <b>true</b> the Collection is sorted by ascending <see cref="Sub.SubID"/></remarks>
		public int Add(short subID) { return Add(new Sub(_groupID, subID)); }
		/// <summary>Adds a new Sub with the provided image</summary>
		/// <param name="subID">ID value for the new Sub</param>
		/// <param name="image">Image to be used, must be <see cref="System.Drawing.Imaging.PixelFormat.Format8bppIndexed"/></param>
		/// <exception cref="System.ArgumentException"><i>image</i> is not formatted properly or <i>subID</i> is already in use</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds allowable dimensions</exception>
		/// <returns>Index of the Sub within the Collection</returns>
		/// <remarks>Sub is added and then if <see cref="AutoSort"/> is <b>true</b> the Collection is sorted by ascending <see cref="Sub.SubID"/></remarks>
		public int Add(short subID, System.Drawing.Bitmap image) { return Add(new Sub(_groupID, subID, image)); }
		/// <summary>Adds a Sub to the Collection</summary>
		/// <param name="sub">The Sub to be added</param>
		/// <exception cref="System.ArgumentException"><i>subID</i> is already in use</exception>
		/// <returns>Index of the Sub within the Collection</returns>
		/// <remarks>Sub is added with the Collection's <see cref="GroupID"/> and then if <see cref="AutoSort"/> is <b>true</b> the Collection is sorted by ascending <see cref="Sub.SubID"/></remarks>
		new public int Add(Sub sub)
		{
			sub.GroupID = _groupID;
			if (GetIndex(sub.SubID) != -1) throw new ArgumentException("Sub ID " + sub.SubID + " already in use");
			_add(sub);
			if (AutoSort) Sort();
			return GetIndex(sub.SubID);
		}

		/// <summary>Empties the Collection of entries</summary>
		/// <remarks>All existing Subs are lost, <see cref="Idmr.Common.FixedSizeCollection{T}.Count"/> is <b>zero</b></remarks>
		public void Clear() { _items.Clear(); }

		/// <summary>Delete the specified Sub from the Collection</summary>
		/// <param name="index">Sub index</param>
		/// <returns><b>true</b> if successful, <b>false</b> for invalid <i>index</i> value</returns>
		public bool Remove(int index) { return (_removeAt(index) != -1); }
		
		/// <summary>Deletes the Sub with the specified ID</summary>
		/// <param name="subID">The ID of the Sub to be deleted</param>
		/// <returns><b>true</b> if successfull, <b>false</b> for invalid <i>subID</i> value</returns>
		public bool RemoveByID(short subID) { return Remove(GetIndex(subID)); }
		
		/// <summary>Updates the Sub.ID</summary>
		/// <param name="subID">The <see cref="Sub.SubID"/> of the Sub to modify</param>
		/// <param name="newID">The new ID</param>
		/// <exception cref="System.ArgumentException"><i>newID</i> is already in use<br/><b>-or</b><br/><i>subID</i> does not exist</exception>
		/// <returns>Index of the updated Sub within the Collection</returns>
		/// <remarks>SubID is updated and then if (see cref="AutoSort"/> is <b>true</b> the Collection is sorted by ascending <see cref="Sub.SubID"/><br/>
		/// This is the preferred method for updating Sub IDs</remarks>
		public int SetSubID(short subID, short newID)
		{
			if (GetIndex(newID) != -1) throw new ArgumentException("Sub ID " + newID + " already in use", "newID");
			int index = GetIndex(subID);
			if (index == -1) throw new ArgumentException("Sub ID " + subID + " does not exist", "subID");
			_items[index].SubID = newID;
			if (AutoSort) Sort();
			return GetIndex(newID);
		}
		
		/// <summary>Gets the Collection index of the Sub with the provided ID</summary>
		/// <param name="subID">Sub ID value</param>
		/// <returns>Collection index if <i>subID</i> exists, otherwise <b>-1</b></returns>
		public int GetIndex(short subID)
		{
			int index;
			for (index = 0; index < Count; index++)
				if (_items[index].SubID == subID) return index;
			return -1;
		}
		
		/// <summary>Sorts the Subs in ascending order by <see cref="Sub.SubID"/></summary>
		public void Sort()
		{
			for(int i = 0; i < Count; i++)
				for(int j = 0; j < Count - 1 - i; j++)
					if (_items[j].SubID > _items[j + 1].SubID)
					{
						Sub temp = _items[j + 1];
						_items[j + 1] = _items[j];
						_items[j] = temp;
					}
		}
		#endregion public methods
		
		#region public properties
		/// <summary>Gets or sets the ID value of the defined parent <see cref="Group"/></summary>
		/// <remarks>Updates all Subs in the Collection.<br/>This is the preferred method of updating <see cref="Sub.GroupID"/></remarks>
		public short GroupID
		{
			get { return _groupID; }
			set
			{
				_groupID = value;
				for(int i = 0; i < Count; i++) _items[i].GroupID = _groupID;
			}
		}
		
		/// <summary>Gets the total number of Colors defined within the Collection</summary>
		/// <remarks>Equals the sum of all child <see cref="Sub.NumberOfColors"/> values</remarks>
		public int NumberOfColors
		{
			get
			{
				int n = 0;
				for (int i = 0; i < Count; i++) n += _items[i].NumberOfColors;
				return n;
			}
		}

		/// <summary>Gets or sets the flag to automatically sort <see cref="Sub">Subs</see> by <see cref="Sub.SubID"/></summary>
		public bool AutoSort { get; set; }
		#endregion public properties
	}
}