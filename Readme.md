# [Idmr.ImageFormat.Dat.dll](https://github.com/MikeG621/ImageFormat-Dat)

Author: [Michael Gaisser](mailto:mjgaisser@gmail.com)  
![GitHub Release](https://img.shields.io/github/v/release/MikeG621/ImageFormat-Dat)
![GitHub Release Date](https://img.shields.io/github/release-date/MikeG621/ImageFormat-Dat)
![GitHub License](https://img.shields.io/github/license/MikeG621/ImageFormat-Dat)

Library for reading LucasArts *.DAT image files.

## Latest Update
#### v2.6 - 24 Apr 2024
 - Added BC3 and BC5 format capability.

---
### Additional Information

#### Dependencies
- [Idmr.Common](https://github.com/MikeG621/Common)
- [JeremyAnsel.BcnSharp](https://github.com/JeremyAnsel/JeremyAnsel.BcnSharp)

#### Instructions:
- Add Idmr.Common.dll and Idmr.ImageFormat.Dat.dll to your references.
- Add both JeremyAnsel.BcnSharpLib32.dll and ~64.dll to the build directory
   for use as external references.

File format structure can be found in the [format file](DAT_Image_File.txt).

Programmer's reference can be found in the [help file](help/Idmr.ImageFormat.Dat.chm).

### Version History

#### v2.5 - 18 Dec 2023
- Added BC7 format capability

#### v2.4 - 27 Feb 2022
- Added Format 25C capability
  - Image isn't decompressed until first accessed
- Format 25 now takes up half the memory after loading

#### v2.3 - 06 Jun 2021
- Empty Groups are now ignored during read
- (GroupCollection) Changed the increment on intial ID creation so it's always negative
- (SubCollection) Added more details to exception message

#### v2.2 - 22 Sep 2019
- Added Format 25 capability
- Removed size limits from images. Masks still need to match the image size.
- (DatFile) Added UsedHeight and UsedWidth properties
- (GroupCollection, SubCollection) Increased ItemLimit to 256
- (GroupCollection, SubCollection) Included an early return to Add if _add fails
- (GroupCollection, SubCollection) Added more quantity checks to better enforce ItemLimit
- Tweaked comments throughout

#### v2.1 - 14 Dec 2014
- Changed license to MPL
- (FrameCollection) SetCount and IsModified implementation
 
#### v2.0.1 - 24 Oct 2012
- (Sub) SetImage uses image.Palette if image is 8bppIndexed
 
#### v2.0 - 05 May 2012
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

---
#### Copyright Information

Copyright (C) Michael Gaisser, 2009-2024  
This library file and related files are licensed under the Mozilla Public License
v2.0 or later. See [License.txt](License.txt) for further details.

The LZMA SDK is written and placed in the public domain by [Igor Pavlov](https://www.7-zip.org/sdk.html).

The BCn library and implementation are Copyright (C) [2020-2021 Richard Geldreich, Jr](https://github.com/richgel999/bc7enc_rdo)
and [2024 Jérémy Ansel](https://github.com/JeremyAnsel/JeremyAnsel.BcnSharp), covered by the MIT License.
See [MIT License.txt](MIT%20License.txt) for full details.

"Star Wars" and related items are trademarks of LucasFilm Ltd and LucasArts Entertainment Co.