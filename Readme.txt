Idmr.ImageFormat.Dat.dll
========================

Author: Michael Gaisser (mjgaisser@gmail.com)
Version: 2.1
Date: 2012.12.14

Library for reading LucasArts *.DAT backdrop files

==========
Version History

v2.1 - 14 Dec 2014
 - Changed license to MPL
 - (FrameCollection) SetCount and IsModified implementation
 
v2.0.1 - 24 Oct 2012
 - (Sub) SetImage uses image.Palette if image is 8bppIndexed
 
v2.0 - 05 May 2012
 - (DatFile) _valEx now says Dat instead of Act >.<
 - (DatFile) Removed _groupsIndexer
 - (DatFile) Groups now simple property, Groups.set is internal
 - (DatFile) Group/Sub converted to class
 - (Group) Removed _subs null checks
 - (GroupCollection, SubCollection) Added AutoSort
 - (GroupCollection, SubCollection) Removed _items null checks, instead uses Count checks
 - (GroupCollection, SubCollection) Now inherits ResizableCollection<>
 - (GroupCollection, SubCollection) _sort renamed to Sort and made public
 - (Sub) Added more null checks throughout
 - (Sub) SetImage uses default 256-color palette is _colors is undefined
 - (Sub) Colors can now be set without a previously defined _image

==========
Additional Information

Instructions:
 - Get latest version of Idmr.Common.dll (v1.1 or later)
 - Add Idmr.Common.dll and Idmr.ImageFormat.Dat.dll to your references

File format structure can be found in DAT_Image_File.txt

Programmer's reference can be found in help/Idmr.ImageFormat.Dat.chm

==========
Copyright Information

Copyright (C) Michael Gaisser, 2009-2014
This library file and related files are licensed under the Mozilla Public License
v2.0 or later.  See MPL.txt for further details.

"Star Wars" and related items are trademarks of LucasFilm Ltd and
LucasArts Entertainment Co.

THESE FILES HAVE BEEN TESTED AND DECLARED FUNCTIONAL, AS SUCH THE AUTHOR CANNOT
BE HELD RESPONSIBLE OR LIABLE FOR UNWANTED EFFECTS DUE ITS USE OR MISUSE. THIS
SOFTWARE IS OFFERED AS-IS WITHOUT WARRANTY OF ANY KIND.