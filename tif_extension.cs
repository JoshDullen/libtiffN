// tif_extension.cs
//
// Based on LIBTIFF, Version 3.9.4 - 15-Jun-2010
// Copyright (c) 2006-2010 by the Authors
// Copyright (c) 1988-1997 Sam Leffler
// Copyright (c) 1991-1997 Silicon Graphics, Inc.
//
// Permission to use, copy, modify, distribute, and sell this software and
// its documentation for any purpose is hereby granted without fee, provided
// that (i) the above copyright notices and this permission notice appear in
// all copies of the software and related documentation, and (ii) the names of
// Sam Leffler and Silicon Graphics may not be used in any advertising or
// publicity relating to the software without the specific, prior written
// permission of Sam Leffler and Silicon Graphics.
//
// THE SOFTWARE IS PROVIDED "AS-IS" AND WITHOUT WARRANTY OF ANY KIND,
// EXPRESS, IMPLIED OR OTHERWISE, INCLUDING WITHOUT LIMITATION, ANY
// WARRANTY OF MERCHANTABILITY OR FITNESS FOR A PARTICULAR PURPOSE.
//
// IN NO EVENT SHALL SAM LEFFLER OR SILICON GRAPHICS BE LIABLE FOR
// ANY SPECIAL, INCIDENTAL, INDIRECT OR CONSEQUENTIAL DAMAGES OF ANY KIND,
// OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
// WHETHER OR NOT ADVISED OF THE POSSIBILITY OF DAMAGE, AND ON ANY THEORY OF
// LIABILITY, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE
// OF THIS SOFTWARE.

// TIFF Library.
//
// Various routines support external extension of the tag set, and other
// application extension capabilities. 

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		public static int TIFFGetTagListCount(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;

			return td.td_customValueCount;
		}

		public static TIFFTAG TIFFGetTagListEntry(TIFF tif, int tag_index)
		{
			TIFFDirectory td=tif.tif_dir;

			if(tag_index<0||tag_index>=td.td_customValueCount) return (TIFFTAG)(-1);
			else return td.td_customValues[tag_index].info.field_tag;
		}

		// This provides read/write access to the TIFFTagMethods within the TIFF
		// structure to application code without giving access to the private
		// TIFF structure.
		public static TIFFTagMethods TIFFAccessTagMethods(TIFF tif)
		{
			return tif.tif_tagmethods;
		}

		public static object TIFFGetClientInfo(TIFF tif, string name)
		{
			foreach(TIFFClientInfoLink link in tif.tif_clientinfo)
			{
				if(link.name==name) return link.data;
			}
			return null;
		}

		public static void TIFFSetClientInfo(TIFF tif, object data, string name)
		{
			// Do we have an existing link with this name? If so, just
			// set it.
			foreach(TIFFClientInfoLink link in tif.tif_clientinfo)
			{
				if(link.name==name)
				{
					link.data=data;
					return;
				}
			}

			// Create a new link.
			TIFFClientInfoLink newlink=new TIFFClientInfoLink();
			newlink.name=name;
			newlink.data=data;

			tif.tif_clientinfo.Add(newlink);
		}
	}
}
