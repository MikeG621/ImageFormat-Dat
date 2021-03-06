﻿/*
 * Idmr.ImageFormat.Dat, Allows editing capability of LucasArts *.DAT Image files
 * Copyright (C) 2009-2021 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in DatFile.cs
 * VERSION: 2.3
 */

/* CHANGE LOG
 * v2.3, 210606
 * [UPD] changed the increment on intial IDs
 * v2.2, 190922
 * [UPD] added a quantity check to ctor
 * [UPD] tweaked comments
 * [UPD] ItemLimit increased to 256
 * [UPD] added early return in Add if _add fails
 * v2.1, 141214
 * [NEW] IsModified implementation
 * [NEW] SetCount
 * [UPD] local Clear removed in favor of Clear in ResizableCollection
 * [UPD] switch to MPL
 * v2.0, 120505
 * [NEW] AutoSort
 * [DEL] _items null checks since Count will be -1
 * [UPD] inherit ResizableCollection<>
 * [UPD] _sort to Sort and public
 */

using System;
using System.Collections.Generic;
using Idmr.Common;

namespace Idmr.ImageFormat.Dat
{
	/// <summary>Object to maintain Dat archive <see cref="Group">Groups</see></summary>
	/// <remarks><see cref="ResizableCollection{T}.ItemLimit"/> is set to <b>200</b></remarks>
	public class GroupCollection : ResizableCollection<Group>
	{
		#region constructors
		/// <summary>Creates an empty Collection</summary>
		public GroupCollection()
		{
			AutoSort = true;
			_itemLimit = 256;
			_items = new List<Group>(_itemLimit);
		}
		/// <summary>Creates a Collection with multiple initial <see cref="Group">Groups</see></summary>
		/// <param name="quantity">Number of <see cref="Group">Groups</see> to start with</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> is non-positive and less than <see cref="ResizableCollection{T}.ItemLimit"/></exception>
		/// <remarks>Individual <see cref="Group">Groups</see> initialized with stand-in <see cref="Group.ID"/> values ascending from <b>-2000</b> and incrementing by <b>100</b></remarks>
		public GroupCollection(int quantity)
		{
			_itemLimit = 256;
			if (quantity < 1 || quantity > _itemLimit) throw new ArgumentOutOfRangeException("DAT.Group quantity must be positive and less than " + _itemLimit);
			_items = new List<Group>(_itemLimit);
			for (int i = 0, id = -2000; i < quantity; i++, id += 1) Add(new Group((short)id));
			AutoSort = true;
		}
		/// <summary>Creates a Collection and populates it with the provided <see cref="Group">Groups</see></summary>
		/// <param name="groups">Initial <see cref="Group">Groups</see></param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="groups"/> has more than 256 elements.</exception>
		public GroupCollection(Group[] groups)
		{
			_itemLimit = 256;
			if (groups.Length > _itemLimit) throw new ArgumentOutOfRangeException("DAT.Group quantity must be less than " + _itemLimit);
			_items = new List<Group>(groups.Length);
			for (int i = 0; i < groups.Length; i++) Add(groups[i]);
		}
		#endregion constructors
		
		#region public methods
		/// <summary>Adds an empty Group with the given ID</summary>
		/// <param name="groupID">ID value for the new Group</param>
		/// <exception cref="ArgumentException"><i>groupID</i> is already in use</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>If <see cref="AutoSort"/> is <b>true</b>, Group is added and then the Collection is sorted by ascending <see cref="Group.ID"/></remarks>
		public int Add(short groupID) { return Add(new Group(groupID)); }
		/// <summary>Adds a Group with the given ID and Subs created from <i>images</i></summary>
		/// <param name="groupID">ID value for the new Group</param>
		/// <param name="images">Images from which to create the Subs</param>
		/// <exception cref="ArgumentException"><i>groupID</i> is already in use<br/><b>-or-</b><br/><i>images</i> are not all 8bppIndexed</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>If <see cref="AutoSort"/> is <b>true</b>, Group is added and then the Collection is sorted by ascending <see cref="Group.ID"/>. <paramref name="images"/> must all be 8bppIndexed and are initialized as <b>ImageType.Transparent</b>.<br/>
		/// To use Blended images, <see cref="Sub">Subs</see> must individually have their <see cref="Sub.Type"/> changed and use <see cref="Sub.SetTransparencyMask"/></remarks>
		public int Add(short groupID, System.Drawing.Bitmap[] images)
		{
			if (GetIndex(groupID) != -1) throw new ArgumentException("Group ID " + groupID + " already in use", "groupID");
			// check that first so time isn't wasted on image processing
			return Add(new Group(groupID, images));
		}
		/// <summary>Adds a Group populated with the given Collection of Subs</summary>
		/// <param name="subs">Subs to be included in the Group</param>
		/// <exception cref="ArgumentException"><see cref="SubCollection.GroupID"/> is already in use</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>If <see cref="AutoSort"/> is <b>true</b>, Group is added and then the Collection is sorted by ascending ID</remarks>
		public int Add(SubCollection subs) { return Add(new Group(subs)); }
		/// <summary>Adds a Group to the Collection</summary>
		/// <param name="group">Group to be added</param>
		/// <exception cref="ArgumentException"><see cref="Group.ID"/> is already in use</exception>
		/// <returns>Index of the Group within the Collection</returns>
		/// <remarks>If <see cref="AutoSort"/> is <b>true</b>, <paramref name="group"/> is added and then the Collection is sorted by ascending <see cref="Group.ID"/></remarks>
		new public int Add(Group group)
		{
			if (GetIndex(group.ID) != -1) throw new ArgumentException("Group ID " + group.ID + " already in use");
			int index = _add(group);
			if (index == -1) return index;
			if (AutoSort) Sort();
			if (!_isLoading) _isModified = true;
			return GetIndex(group.ID);
		}

		/// <summary>Deletes the specified Group from the Collection</summary>
		/// <param name="index">Group index</param>
		/// <returns><b>true</b> if successful, <b>false</b> for invalid <paramref name="index"/> value</returns>
		public bool Remove(int index)
		{
			bool success = (_removeAt(index) != -1);
			if (success) _isModified = true;
			return success;
		}

		/// <summary>Deletes the Group with the specified ID</summary>
		/// <param name="groupID">The ID of the Group to be deleted</param>
		/// <returns><b>true</b> if successfull, <b>false</b> for invalid <paramref name="groupID"/> value</returns>
		public bool RemoveByID(short groupID) { return Remove(GetIndex(groupID)); }

		/// <summary>Updates the <see cref="Group.ID"/></summary>
		/// <param name="groupID">The ID of the Group to modify</param>
		/// <param name="newID">The new ID</param>
		/// <exception cref="ArgumentException"><paramref name="newID"/> is already in use<br/><b>-or-</b><br/><paramref name="groupID"/> does not exist</exception>
		/// <returns>Index of the updated Group within the Collection</returns>
		/// <remarks><see cref="Group.ID"/> is updated and then if <see cref="AutoSort"/> is <b>true</b> the Collection is sorted by ascending ID.<br/>
		/// This is the preferred method of updating Group IDs</remarks>
		public int SetGroupID(short groupID, short newID)
		{
			if (GetIndex(newID) != -1) throw new ArgumentException("Group ID " + newID + " already in use", "newID");
			int index = GetIndex(groupID);
			if (index == -1) throw new ArgumentException("Group ID " + groupID + " does not exist", "groupID");
			_items[index].ID = newID;
			if (AutoSort) Sort();
			if (!_isLoading) _isModified = true;
			return GetIndex(newID);
		}

		/// <summary>Gets the Collection index of the Group with the provided ID</summary>
		/// <param name="groupID">Group ID value</param>
		/// <returns>Collection index if <paramref name="groupID"/> exists, otherwise <b>-1</b></returns>
		public int GetIndex(short groupID)
		{
			int index;
			for (index = 0; index < Count; index++)
				if (_items[index].ID == groupID) return index;
			return -1;
		}
		
		/// <summary>Sorts the Groups in ascending order by <see cref="Group.ID"/></summary>
		public void Sort()
		{
			for(int i = 0; i < Count; i++)
				for(int j = 0; j < Count - 1 - i; j++)
					if (_items[j].ID > _items[j + 1].ID)
					{
						Group temp = _items[j + 1];
						_items[j + 1] = _items[j];
						_items[j] = temp;
					}
			if (!_isLoading) _isModified = true;
		}

		/// <summary>Expands or contracts the Collection, populating as necessary</summary>
		/// <param name="value">The new size of the Collection. Must not be negative.</param>
		/// <param name="allowTruncate">Controls if the Collection is allowed to get smaller</param>
		/// <exception cref="InvalidOperationException"><paramref name="value"/> is smaller than <see cref="FixedSizeCollection{T}.Count"/> and <paramref name="allowTruncate"/> is <b>false</b>.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 0.</exception>
		/// <remarks>If the Collection expands, the new items will be a blank <see cref="Group"/> with incremental <see cref="Group.ID"/> values start from <b>-3000</b>. When truncating, items will be removed starting from the last index.</remarks>
		public override void SetCount(int value, bool allowTruncate)
		{
			if (value == Count) return;
			else if (value < 0) throw new ArgumentOutOfRangeException("value", "value must not be negative");
			else if (value == 0) Clear();
			else if (value < Count)
			{
				if (!allowTruncate) throw new InvalidOperationException("Reducing 'value' will cause data loss");
				else while (Count > value) _removeAt(Count - 1);
			}
			else
			{
				short newId = -3000;
				while (Count < value)
				{
					while (GetIndex(newId) != -1) newId++;
					Add(new Group(newId++));
				}
				if (AutoSort) Sort();
			}
			if (!_isLoading) _isModified = true;
		}
		#endregion public methods
		
		#region public properties
		/// <summary>Gets the total number of colors defined in the Collection</summary>
		/// <remarks>Equals the sum of all child <see cref="SubCollection.NumberOfColors"/> values</remarks>
		public int NumberOfColors
		{
			get
			{
				int n = 0;
				for(int i = 0; i <  Count; i++) n += _items[i].Subs.NumberOfColors;
				return n;
			}
		}
		
		/// <summary>Gets the total number of Subs defined in the Collection</summary>
		/// <remarks>Equals the sum of all child <see cref="Group.NumberOfSubs"/> values</remarks>
		public short NumberOfSubs
		{
			get
			{
				short n = 0;
				for (int i = 0; i <  Count; i++) n += _items[i].NumberOfSubs;
				return n;
			}
		}

		/// <summary>Gets or sets if the Groups should automatically sort in ascending order when adding a Group</summary>
		/// <remarks>Default is <b>true</b> when creating a new Dat, <b>false</b> when loading from file</remarks>
		public bool AutoSort { get; set; }
		#endregion public properties
	}
}