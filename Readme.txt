Idmr.ImageFormat.Dat.dll
========================

Author: Michael Gaisser (mjgaisser@gmail.com)
Version: 2.4
Date: 2022.02.27

Library for reading LucasArts *.DAT backdrop files

==========
Version History

 - BC7 format detection, currently unsupported and returns a blank image

v2.4 - 27 Feb 2022
 - Added Format 25C capability
   - Image isn't decompressed until first accessed
 - Format 25 now takes up half the memory after loading

v2.3 - 06 Jun 2021
 - Empty Groups are now ignored during read
 - (GroupCollection) Changed the increment on intial ID creation so it's always negative
 - (SubCollection) Added more details to exception message

v2.2 - 22 Sep 2019
 - Added Format 25 capability
 - Removed size limits from images. Masks still need to match the image size.
 - (DatFile) Added UsedHeight and UsedWidth properties
 - (GroupCollection, SubCollection) Increased ItemLimit to 256
 - (GroupCollection, SubCollection) Included an early return to Add if _add fails
 - (GroupCollection, SubCollection) Added more quantity checks to better enforce ItemLimit
 - Tweaked comments throughout

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

Copyright (C) Michael Gaisser, 2009-2022
This library file and related files are licensed under the Mozilla Public License
v2.0 or later.  See License.txt for further details.

The LZMA SDK is written and placed in the public domain by Igor Pavlov.

"Star Wars" and related items are trademarks of LucasFilm Ltd and
LucasArts Entertainment Co.

THESE FILES HAVE BEEN TESTED AND DECLARED FUNCTIONAL, AS SUCH THE AUTHOR CANNOT
BE HELD RESPONSIBLE OR LIABLE FOR UNWANTED EFFECTS DUE ITS USE OR MISUSE. THIS
SOFTWARE IS OFFERED AS-IS WITHOUT WARRANTY OF ANY KIND.