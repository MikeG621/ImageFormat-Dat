/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LucasArts *.DAT Image files
 * Copyright (C) 2009-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in DatFile.cs
 */

using System;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object to maintain Dat archive Groups</summary>
	public class GroupCollection
	{
		DatFile.Group[] _items = null;
		int _count = 0;
		
		#region constructors
		/// <summary>Creates an empty Collection</summary>
		public GroupCollection()
		{
		}
		/// <summary>Creates a Collection with multiple initial Groups</summary>
		/// <param name="quantity">Number of Groups to start with</param>
		/// <exception cref="System.ArgumentException"><i>quantity</i> is not positive</exception>
		/// <remarks>Individual Groups remain uninitialized</remarks>
		public GroupCollection(int quantity)
		{
			if (quantity < 1) throw new ArgumentException("quantity must be positive", "quantity");
			_count = quantity;
			_items = new DatFile.Group[_count];
		}
		/// <summary>Creates a Collection and populates it with the provided Groups</summary>
		/// <param name="groups">Initial Groups</param>
		/// <remarks>Groups will be sorted by ascending ID</remarks>
		public GroupCollection(DatFile.Group[] groups)
		{
			_count = groups.Length;
			_items = groups;
			_sort();
		}
		#endregion constructors
		
		#region public methods
		/// <summary>Adds an empty Group with the given ID</summary>
		/// <param name="groupID">ID value for the new Group</param>
		/// <exception cref="System.ArgumentException">groupID is already in use</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>Group is added and then the Collection is sorted by ascending ID</remarks>
		public int Add(short groupID) { return _add(new DatFile.Group(groupID)); }
		/// <summary>Adds a Group with the given ID and Subs created from <i>images</i></summary>
		/// <param name="groupID">ID value for the new Group</param>
		/// <param name="images">Images from which to create the Subs</param>
		/// <exception cref="System.ArgumentException"><i>groupID</i> is already in use, or <i>images</i> are not all 8bppIndexed</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>Group is added and then the Collection is sorted by ascending ID. <i>images</i> must all be 8bppIndexed and are initialized as ImageType.Transparent.<br>
		/// To use Blended images, Subs must individually have their Type changed and use SetTransparencyMask( )</remarks>
		public int Add(short groupID, System.Drawing.Bitmap[] images)
		{
			if (GetIndex(groupID) != -1) throw new ArgumentException("Group ID " + groupID + " already in use", "groupID");
			// check that first so time isn't wasted on image processing
			return _add(new DatFile.Group(groupID, images));
		}
		/// <summary>Adds a Group populated with the given Collection of Subs</summary>
		/// <param name="subs">Subs to be included in the Group</param>
		/// <exception cref="System.ArgumentException">Group.ID is already in use</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>Group is added and then the Collection is sorted by ascending SubID</remarks>
		public int Add(SubCollection subs) { return _add(new DatFile.Group(subs)); }
		/// <summary>Adds a Group to the Collection</summary>
		/// <param name="group">Group to be added</param>
		/// <exception cref="System.ArgumentException">Group.ID is already in use</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>Group is added and then the Collection is sorted by ascending ID</remarks>
		public int Add(DatFile.Group group) { return _add(group); }
		
		/// <summary>Empties the Collection of entries</summary>
		/// <remarks>All existing Groups are lost, Count is set to zero</remarks>
		public void Clear()
		{
			_count = 0;
			_items = null;
		}

		/// <summary>Deletes the specified Group from the Collection</summary>
		/// <param name="index">Group index</param>
		/// <returns><i>true</i> if successful, <i>false</i> for invalid <i>index</i> value</returns>
		/// <remarks>If only Group is specified, executes Clear( )</remarks>
		public bool Remove(int index) { return _remove(index); }
		
		/// <summary>Deletes the Group with the specified ID</summary>
		/// <param name="groupID">The ID of the Group to be deleted</param>
		/// <returns><i>true</i> if successfull, <i>false</i> for invalid <i>groupID</i> value</returns>
		/// <remarks>If only Group is specified, executes Clear()</remarks>
		public bool RemoveByID(short groupID) { return _remove(GetIndex(groupID)); }
		
		/// <summary>Updates the Group.ID</summary>
		/// <param name="groupID">The existing Group.ID</param>
		/// <param name="newID">The new Group.ID</param>
		/// <exception cref="System.ArgumentException"><i>newID</i> is already in use or <i>groupID</i> does not exist</exception>
		/// <returns>Index of the updated Group within the Collection</returns>
		/// <remarks>Group.ID is updated and then the Collection is sorted by ascending ID<br>
		/// This is the preferred method for updating Group IDs</remarks>
		public int SetGroupID(short groupID, short newID)
		{
			if (GetIndex(newID) != -1) throw new ArgumentException("Group ID " + newID + " already in use", "newID");
			int index = GetIndex(groupID);
			if (index == -1) throw new ArgumentException("Group ID " + groupID + " does not exist", "groupID");
			_items[index].ID = newID;
			_sort();
			return GetIndex(newID);
		}
		
		/// <summary>Gets the Collection index of the Group with the provided ID</summary>
		/// <param name="groupID">Group ID value</param>
		/// <returns>Collection index if <i>groupID</i> exists, otherwise -1</returns>
		public int GetIndex(short groupID)
		{
			int index;
			for (index = 0; index < _count; index++)
				if (_items[index].ID == groupID) return index;
			return -1;
		}
		#endregion public methods
		
		#region public properties
		/// <summary>A single Group within the Collection</summary>
		public DatFile.Group this[int index]
		{
			get { return _items[index]; }
			set { _items[index] = value; }
		}
		
		/// <summary>Gets the number of objects in the Collection</summary>
		public int Count { get { return _count; } }
		
		/// <summary>Gets the total number of colors defined in the Collection</summary>
		/// <remarks>Equals the sum of <i>this[].NumberOfColors</i> values</remarks>
		public int NumberOfColors
		{
			get
			{
				int n = 0;
				for(int i = 0; i < (_items != null ? _count : -1); i++) n += _items[i].Subs.NumberOfColors;
				return n;
			}
		}
		
		/// <summary>Gets the total number of Subs defined in the file</summary>
		/// <remarks>Equals the sum of <i>Groups[].NumberOfSubs</i> values</remarks>
		public short NumberOfSubs
		{
			get
			{
				short n = 0;
				for (int i = 0; i < (_items != null ? _count : -1); i++) n += _items[i].NumberOfSubs;
				return n;
			}
		}
		#endregion public properties
		
		#region private methods
		void _sort()
		{
			for(int i = 0; i < _count; i++)
				for(int j = 0; j < _count - 1 - i; j++)
					if (_items[j].ID > _items[j + 1].ID)
					{
						DatFile.Group temp = _items[j + 1];
						_items[j + 1] = _items[j];
						_items[j] = temp;
					}
		}
		
		bool _remove(int index)
		{
			if (index < 0 || index >= _count) return false;
			if (index == 0 && _count == 1) { Clear(); return true; }
			_count--;
			DatFile.Group[] tempItems = _items;
			_items = new DatFile.Group[_count];
			for (int i = 0; i < index; i++) _items[i] = tempItems[i];
			for (int i = index; i < _count; i++) _items[i] = tempItems[i + 1];
			return true;
		}
		
		int _add(DatFile.Group item)
		{
			if (GetIndex(item.ID) != -1) throw new ArgumentException("Group ID " + item.ID + " already in use");
			DatFile.Group[] tempItems = _items;
			_items = new DatFile.Group[_count + 1];
			for (int i = 0; i < _count; i++) _items[i] = tempItems[i];
			_items[_count] = item;
			_count++;
			_sort();
			return GetIndex(item.ID);
		}
		#endregion private methods
	}
}