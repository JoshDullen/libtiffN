// tif_compress.cs
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
// Compression Scheme Configuration Support.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		static bool TIFFNoEncode(TIFF tif, string method)
		{
			TIFFCodec c=TIFFFindCODEC(tif.tif_dir.td_compression);

			if(c!=null) TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0} {1} encoding is not implemented", c.name, method);
			else TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Compression scheme {0} {1} encoding is not implemented", tif.tif_dir.td_compression, method);

			return false;
		}

		static bool TIFFNoRowEncode(TIFF tif, byte[] pp, int cc, ushort s)
		{
			return TIFFNoEncode(tif, "scanline");
		}

		static bool TIFFNoStripEncode(TIFF tif, byte[] pp, int cc, ushort s)
		{
			return TIFFNoEncode(tif, "strip");
		}

		static bool TIFFNoTileEncode(TIFF tif, byte[] pp, int cc, ushort s)
		{
			return TIFFNoEncode(tif, "tile");
		}

		static bool TIFFNoDecode(TIFF tif, string method)
		{
			TIFFCodec c=TIFFFindCODEC(tif.tif_dir.td_compression);

			if(c!=null) TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0} {1} decoding is not implemented", c.name, method);
			else TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Compression scheme {0} {1} decoding is not implemented", tif.tif_dir.td_compression, method);

			return false;
		}

		static bool TIFFNoRowDecode(TIFF tif, byte[] pp, int cc, ushort s)
		{
			return TIFFNoDecode(tif, "scanline");
		}

		static bool TIFFNoStripDecode(TIFF tif, byte[] pp, int cc, ushort s)
		{
			return TIFFNoDecode(tif, "strip");
		}

		static bool TIFFNoTileDecode(TIFF tif, byte[] pp, int cc, ushort s)
		{
			return TIFFNoDecode(tif, "tile");
		}

		static bool TIFFNoSeek(TIFF tif, uint off)
		{
			//TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Compression algorithm does not support random access");
			return false;
		}

		static bool TIFFNoPreCode(TIFF tif, ushort sampleNumber)
		{
			return true;
		}

		static bool TIFFtrue(TIFF tif)
		{
			return true;
		}
		
		static void TIFFvoid(TIFF tif)
		{
		}

		static void TIFFSetDefaultCompressionState(TIFF tif)
		{
			tif.tif_decodestatus=true;
			tif.tif_setupdecode=TIFFtrue;
			tif.tif_predecode=TIFFNoPreCode;
			tif.tif_decoderow=TIFFNoRowDecode;
			tif.tif_decodestrip=TIFFNoStripDecode;
			tif.tif_decodetile=TIFFNoTileDecode;
			tif.tif_encodestatus=true;
			tif.tif_setupencode=TIFFtrue;
			tif.tif_preencode=TIFFNoPreCode;
			tif.tif_postencode=TIFFtrue;
			tif.tif_encoderow=TIFFNoRowEncode;
			tif.tif_encodestrip=TIFFNoStripEncode;
			tif.tif_encodetile=TIFFNoTileEncode;
			tif.tif_close=TIFFvoid;
			tif.tif_seek=TIFFNoSeek;
			tif.tif_cleanup=TIFFvoid;
			tif.tif_defstripsize=_TIFFDefaultStripSize;
			tif.tif_deftilesize=_TIFFDefaultTileSize;
			tif.tif_flags&=~(TIF_FLAGS.TIFF_NOBITREV|TIF_FLAGS.TIFF_NOREADRAW);
		}

		public static bool TIFFSetCompressionScheme(TIFF tif, COMPRESSION scheme)
		{
			TIFFCodec c=TIFFFindCODEC((COMPRESSION)scheme);

			TIFFSetDefaultCompressionState(tif);
			
			// Don't treat an unknown compression scheme as an error.
			// This permits applications to open files with data that
			// the library does not have builtin support for, but which
			// may still be meaningful.
			return (c!=null?c.init(tif, scheme):true);
		}
		
		// Other compression schemes may be registered. Registered
		// schemes can also override the builtin versions provided
		// by this library.
		static readonly List<TIFFCodec> registeredCODECS=new List<TIFFCodec>();

		public static TIFFCodec TIFFFindCODEC(COMPRESSION scheme)
		{
			foreach(TIFFCodec c in registeredCODECS) if(c.scheme==scheme) return c;
			foreach(TIFFCodec c in TIFFBuiltinCODECS) if(c.scheme==scheme) return c;
			return null;
		}

		public static TIFFCodec TIFFRegisterCODEC(COMPRESSION scheme, string name, TIFFInitMethod init)
		{
			try
			{
				registeredCODECS.Add(new TIFFCodec(name, scheme, init));
			}
			catch
			{
				TIFFError("TIFFRegisterCODEC", "No space to register compression scheme {0}", name);
				return null;
			}
			return registeredCODECS[registeredCODECS.Count-1];
		}

		public static void TIFFUnRegisterCODEC(TIFFCodec c)
		{
			if(registeredCODECS.Remove(c)) return;
			TIFFError("TIFFUnRegisterCODEC", "Cannot remove compression scheme {0}; not registered", c.name);
		}

		// **************************************************************************
		// *							TIFFGetConfisuredCODECs()					*
		// **************************************************************************

		// Get list of configured codecs, both built-in and registered by user.
		//
		// @return returns List of TIFFCodec records (the last record should be NULL)
		// or null if function failed.
		public static List<TIFFCodec> TIFFGetConfiguredCODECs()
		{
			try
			{
				List<TIFFCodec> codecs=new List<TIFFCodec>();
				foreach(TIFFCodec c in registeredCODECS) codecs.Add(new TIFFCodec(c.name, c.scheme, c.init));
				foreach(TIFFCodec c in TIFFBuiltinCODECS)
					if(TIFFIsCODECConfigured(c.scheme)) codecs.Add(new TIFFCodec(c.name, c.scheme, c.init));

				return codecs;
			}
			catch
			{
				return null;
			}
		}
	}
}
