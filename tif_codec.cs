// tif_codec.cs
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

// TIFF Library
//
// Builtin Compression Scheme Configuration Support.

using System.Collections.Generic;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// Compression schemes statically built into the library.
		static readonly List<TIFFCodec> TIFFBuiltinCODECS=MakeTIFFBuiltinCODECSList();

		static List<TIFFCodec> MakeTIFFBuiltinCODECSList()
		{
			List<TIFFCodec> ret=new List<TIFFCodec>();

			ret.Add(new TIFFCodec("None", COMPRESSION.NONE, TIFFInitDumpMode));

#if LZW_SUPPORT
			ret.Add(new TIFFCodec("LZW", COMPRESSION.LZW, TIFFInitLZW));
#else
			ret.Add(new TIFFCodec("LZW", COMPRESSION.LZW, NotConfigured));
#endif

#if PACKBITS_SUPPORT
			ret.Add(new TIFFCodec("PackBits", COMPRESSION.PACKBITS, TIFFInitPackBits));
#else
			ret.Add(new TIFFCodec("PackBits", COMPRESSION.PACKBITS, NotConfigured));
#endif

#if THUNDER_SUPPORT
			ret.Add(new TIFFCodec("ThunderScan", COMPRESSION.THUNDERSCAN, TIFFInitThunderScan));
#else
			ret.Add(new TIFFCodec("ThunderScan", COMPRESSION.THUNDERSCAN, NotConfigured));
#endif

#if NEXT_SUPPORT
			ret.Add(new TIFFCodec("NeXT", COMPRESSION.NEXT, TIFFInitNeXT));
#else
			ret.Add(new TIFFCodec("NeXT", COMPRESSION.NEXT, NotConfigured));
#endif

#if JPEG_SUPPORT
			ret.Add(new TIFFCodec("JPEG", COMPRESSION.JPEG, TIFFInitJPEG));
#else
			ret.Add(new TIFFCodec("JPEG", COMPRESSION.JPEG, NotConfigured));
#endif

#if OJPEG_SUPPORT
			ret.Add(new TIFFCodec("Old-style JPEG", COMPRESSION.OJPEG, TIFFInitOJPEG));
#else
			ret.Add(new TIFFCodec("Old-style JPEG", COMPRESSION.OJPEG, NotConfigured));
#endif

#if CCITT_SUPPORT
			ret.Add(new TIFFCodec("CCITT RLE", COMPRESSION.CCITTRLE, TIFFInitCCITTRLE));
			ret.Add(new TIFFCodec("CCITT RLE/W", COMPRESSION.CCITTRLEW, TIFFInitCCITTRLEW));
			ret.Add(new TIFFCodec("CCITT Group 3", COMPRESSION.CCITTFAX3, TIFFInitCCITTFax3));
			ret.Add(new TIFFCodec("CCITT Group 4", COMPRESSION.CCITTFAX4, TIFFInitCCITTFax4));
#else
			ret.Add(new TIFFCodec("CCITT RLE", COMPRESSION.CCITTRLE, NotConfigured));
			ret.Add(new TIFFCodec("CCITT RLE/W", COMPRESSION.CCITTRLEW, NotConfigured));
			ret.Add(new TIFFCodec("CCITT Group 3", COMPRESSION.CCITTFAX3, NotConfigured));
			ret.Add(new TIFFCodec("CCITT Group 4", COMPRESSION.CCITTFAX4, NotConfigured));
#endif

#if JBIG_SUPPORT
			ret.Add(new TIFFCodec("ISO JBIG", COMPRESSION.JBIG, TIFFInitJBIG));
#else
			ret.Add(new TIFFCodec("ISO JBIG", COMPRESSION.JBIG, NotConfigured));
#endif

#if ZIP_SUPPORT
			ret.Add(new TIFFCodec("Deflate", COMPRESSION.DEFLATE, TIFFInitZIP));
			ret.Add(new TIFFCodec("AdobeDeflate", COMPRESSION.ADOBE_DEFLATE, TIFFInitZIP));
#else
			ret.Add(new TIFFCodec("Deflate", COMPRESSION.DEFLATE, NotConfigured));
			ret.Add(new TIFFCodec("AdobeDeflate", COMPRESSION.ADOBE_DEFLATE, NotConfigured));
#endif

#if PIXARLOG_SUPPORT
			ret.Add(new TIFFCodec("PixarLog", COMPRESSION.PIXARLOG, TIFFInitPixarLog));
#else
			ret.Add(new TIFFCodec("PixarLog", COMPRESSION.PIXARLOG, NotConfigured));
#endif

#if LOGLUV_SUPPORT
			ret.Add(new TIFFCodec("SGILog", COMPRESSION.SGILOG, TIFFInitSGILog));
			ret.Add(new TIFFCodec("SGILog24", COMPRESSION.SGILOG24, TIFFInitSGILog));
#else
			ret.Add(new TIFFCodec("SGILog", COMPRESSION.SGILOG, NotConfigured));
			ret.Add(new TIFFCodec("SGILog24", COMPRESSION.SGILOG24, NotConfigured));
#endif
			return ret;
		}

		static bool _notConfigured(TIFF tif)
		{
			TIFFCodec c=TIFFFindCODEC(tif.tif_dir.td_compression);
			TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0} compression support is not configured", (c!=null?c.name:tif.tif_dir.td_compression.ToString()));
			return false;
		}

		static bool NotConfigured(TIFF tif, COMPRESSION scheme)
		{
			tif.tif_decodestatus=false;
			tif.tif_setupdecode=_notConfigured;
			tif.tif_encodestatus=false;
			tif.tif_setupencode=_notConfigured;
			return false;
		}

		// **************************************************************************
		// *							TIFFIsCODECConfigured()						*
		// **************************************************************************

		// Check whether we have working codec for the specific coding scheme.
		//
		// @return returns true if the codec is configured and working. Otherwise
		// false will be returned.
		public static bool TIFFIsCODECConfigured(COMPRESSION scheme)
		{
			TIFFCodec codec=TIFFFindCODEC(scheme);

			if(codec==null) return false;
			if(codec.init==null) return false;
			return codec.init!=NotConfigured;
		}
	}
}
