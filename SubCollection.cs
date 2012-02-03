/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LucasArts *.DAT Image files
 * Copyright (C) 2009-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in DatFile.cs
 */

using System;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object to maintain Dat archive Subs</summary>
	public class SubCollection
	{
		DatFile.Sub[] _items = null;
		int _count = 0;
		short _groupID = 0;
		
		#region constructors
		/// <summary>Creates an empty Collection</summary>
		/// <param name="groupID">ID value of the parent Group</param>
		public SubCollection(short groupID)
		{
			_groupID = groupID;
		}
		/// <summary>Creates a Collection with multiple initial Subs</summary>
		/// <param name="quantity">Number of Subs to start with</param>
		/// <param name="groupID">ID value of the parent Group</param>
		/// <exception cref="System.ArgumentException"><i>quantity</i> is not positive</exception>
		/// <remarks>Individual Subs are created with <i>groupID</i> and SubIDs starting from zero. Sub colors and images remain uninitialized</remarks>
		public SubCollection(int quantity, short groupID)
		{
			if (quantity < 1) throw new ArgumentException("quantity must be positive", "quantity");
			_count = quantity;
			_items = new DatFile.Sub[_count];
			_groupID = groupID;
			for (short i = 0; i < _count; i++) _items[i] = new DatFile.Sub(_groupID, i);
		}
		/// <summary>Creates a Collection and populates it with the provided Subs</summary>
		/// <param name="subs">Initial Subs</param>
		/// <remarks>GroupID defined by first Sub. Subs will be sorted by ascending SubID</remarks>
		public SubCollection(DatFile.Sub[] subs)
		{
			_count = subs.Length;
			_items = subs;
			GroupID = subs[0].GroupID;
			_sort();
		}
		/// <summary>Creates a Collection and populates it with Subs created from the provided images</summary>
		/// <param name="groupID">ID value of the parent Group</param>
		/// <param name="images">Images from which to create the Subs</param>
		/// <remarks><i>images</i> must all be 8bppIndexed, 640x480 max. Initialized as ImageType.Transparent. If a Blended type is desired, change Type and SetTransparencyMask( )</remarks>
		public SubCollection(short groupID, System.Drawing.Bitmap[] images)
		{
			_count = images.Length;
			_items = new DatFile.Sub[_count];
			_groupID = groupID;
			for(short i = 0; i < _count; i++) _items[i] = new DatFile.Sub(groupID, i, images[i]);
		}
		#endregion constructors
		
		#region public methods
		/// <summary>Adds an empty Sub</summary>
		/// <param name="subID">ID value for the new Sub</param>
		/// <exception cref="System.ArgumentException"><i>subID</i> is already in use</exception>
		/// <returns>Index of the Sub within the Collection</returns>
		/// <remarks>Sub is added and then the Collection is sorted by ascending SubID</remarks>
		public int Add(short subID) { return _add(new DatFile.Sub(_groupID, subID)); }
		/// <summary>Adds a new Sub with the provided image</summary>
		/// <param name="subID">ID value for the new Sub</param>
		/// <param name="image">Image to be used, must be 8bppIndexed</param>
		/// <exception cref="System.ArgumentException"><i>image</i> is not formatted properly or <i>subID</i> is already in use</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds allowable dimensions</exception>
		/// <returns>Index of the Sub within the Collection</returns>
		/// <remarks>Sub is added and then the Collection is sorted by ascending SubID</remarks>
		public int Add(short subID, System.Drawing.Bitmap image) { return _add(new DatFile.Sub(_groupID, subID, image)); }
		/// <summary>Adds a Sub to the Collection</summary>
		/// <param name="sub">The Sub to be added</param>
		/// <exception cref="System.ArgumentException"><i>subID</i> is already in use</exception>
		/// <returns>Index of the Sub within the Collection</returns>
		/// <remarks>Sub is added with the Collection's GroupID and then the Collection is sorted by ascending SubID</remarks>
		public int Add(DatFile.Sub sub)
		{
			sub.GroupID = _groupID;
			return _add(sub);
		}

		/// <summary>Empties the Collection of entries</summary>
		/// <remarks>All existing Subs are lost, Count is set to zero</remarks>
		public void Clear()
		{
			_count = 0;
			_items = null;
		}

		/// <summary>Delete the specified Sub from the Collection</summary>
		/// <param name="index">Sub index</param>
		/// <returns><i>true</i> if successful, <i>false</i> for invalid <i>index</i> value</returns>
		/// <remarks>If only Sub is specified, executes Clear( )</remarks>
		public bool Remove(int index) { return _remove(index); }
		
		/// <summary>Deletes the Sub with the specified ID</summary>
		/// <param name="subID">The ID of the Sub to be deleted</param>
		/// <returns><i>true</i> if successfull, <i>false</i> for invalid <i>subID</i> value</returns>
		/// <remarks>If only Sub is specified, executes Clear( )</remarks>
		public bool RemoveByID(short subID) { return _remove(GetIndex(subID)); }
		
		/// <summary>Updates the Sub.ID</summary>
		/// <param name="subID">The existing Sub.ID</param>
		/// <param name="newID">The new Sub.ID</param>
		/// <exception cref="System.ArgumentException"><i>newID</i> is already in use or <i>subID</i> does not exist</exception>
		/// <returns>Index of the updated Sub within the Collection</returns>
		/// <remarks>SubID is updated and then the Collection is sorted by ascending SubID<br>
		/// This is the preferred method for updating Sub IDs</remarks>
		public int SetSubID(short subID, short newID)
		{
			if (GetIndex(newID) != -1) throw new ArgumentException("Sub ID " + newID + " already in use", "newID");
			int index = GetIndex(subID);
			if (index == -1) throw new ArgumentException("Sub ID " + subID + " does not exist", "subID");
			_items[index].SubID = newID;
			_sort();
			return GetIndex(newID);
		}
		
		/// <summary>Gets the Collection index of the Sub with the provided ID</summary>
		/// <param name="subID">Sub ID value</param>
		/// <returns>Collection index if <i>subID</i> exists, otherwise -1</returns>
		public int GetIndex(short subID)
		{
			int index;
			for (index = 0; index < _count; index++)
				if (_items[index].SubID == subID) return index;
			return -1;
		}
		#endregion public methods
		
		#region public properties
		/// <summary>A single Sub within the Collection</summary>
		public DatFile.Sub this[int index]
		{
			get { return _items[index]; }
			set { _items[index] = value; }
		}
		
		/// <summary>Gets the number of objects in the Collection</summary>
		public int Count { get { return _count; } }
		
		/// <summary>Gets or sets the ID value of the defined parent Group</summary>
		public short GroupID
		{
			get { return _groupID; }
			set
			{
				_groupID = value;
				for(int i = 0; i < _count; i++) _items[i].GroupID = _groupID;
			}
		}
		
		/// <summary>Gets the total number of Colors defined within the Collection</summary>
		/// <remarks>Equals the sum of <i>this[].NumberOfColors</i> values</remarks>
		public int NumberOfColors
		{
			get
			{
				int n = 0;
				for (int i = 0; i < (_items != null ? _count : -1); i++) n += _items[i].NumberOfColors;
				return n;
			}
		}
		#endregion public properties
		
		#region private methods
		void _sort()
		{
			for(int i = 0; i < _count; i++)
				for(int j = 0; j < _count - 1 - i; j++)
					if (_items[j].SubID > _items[j + 1].SubID)
					{
						DatFile.Sub temp = _items[j + 1];
						_items[j + 1] = _items[j];
						_items[j] = temp;
					}
		}
		
		bool _remove(int index)
		{
			if (index < 0 || index >= _count) return false;
			if (index == 0 && _count == 1) { Clear(); return true; }
			_count--;
			DatFile.Sub[] tempItems = _items;
			_items = new DatFile.Sub[_count];
			for (int i = 0; i < index; i++) _items[i] = tempItems[i];
			for (int i = index; i < _count; i++) _items[i] = tempItems[i + 1];
			return true;
		}
		
		int _add(DatFile.Sub item)
		{
			if (GetIndex(item.SubID) != -1) throw new ArgumentException("Sub ID " + item.SubID + " already in use");
			DatFile.Sub[] tempItems = _items;
			_items = new DatFile.Sub[_count + 1];
			for (int i = 0; i < _count; i++) _items[i] = tempItems[i];
			_items[_count] = item;
			_count++;
			_sort();
			return GetIndex(item.SubID);
		}
		#endregion private methods
	}
}