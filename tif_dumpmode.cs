// tif_dumpmode.cs
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

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// Encode a hunk of pixels.
		static bool DumpModeEncode(TIFF tif, byte[] buf, int cc, ushort s)
		{
			int bufInd=0;
			while(cc>0)
			{
				int n=cc;

				if((tif.tif_rawcc+n)>tif.tif_rawdatasize) n=(int)(tif.tif_rawdatasize-tif.tif_rawcc);

#if DEBUG
				if(n<=0) throw new Exception("n<=0");
#endif

				// Avoid copy if client has setup raw
				// data buffer to avoid extra copy.
				if(tif.tif_rawcp!=0||buf!=tif.tif_rawdata) Array.Copy(buf, bufInd, tif.tif_rawdata, tif.tif_rawcp, n);

				tif.tif_rawcp+=(uint)n;
				tif.tif_rawcc+=(uint)n;
				bufInd+=n;
				cc-=n;

				if((tif.tif_rawcc>=tif.tif_rawdatasize)&&!TIFFFlushData1(tif)) return false;
			}

			return true;
		}

		// Decode a hunk of pixels.
		static bool DumpModeDecode(TIFF tif, byte[] buf, int cc, ushort s)
		{
			if(tif.tif_rawcc<cc)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "DumpModeDecode: Not enough data for scanline {0}", tif.tif_row);
				return false;
			}

			// Avoid copy if client has setup raw
			// data buffer to avoid extra copy.
			if(tif.tif_rawcp!=0||buf!=tif.tif_rawdata) Array.Copy(tif.tif_rawdata, tif.tif_rawcp, buf, 0, cc);

			tif.tif_rawcp+=(uint)cc;
			tif.tif_rawcc-=(uint)cc;

			return true;
		}

		// Seek forwards nrows in the current strip.
		static bool DumpModeSeek(TIFF tif, uint nrows)
		{
			tif.tif_rawcp+=nrows*tif.tif_scanlinesize;
			tif.tif_rawcc-=nrows*tif.tif_scanlinesize;

			return true;
		}

		// Initialize dump mode.
		static bool TIFFInitDumpMode(TIFF tif, COMPRESSION scheme)
		{
			tif.tif_decoderow=DumpModeDecode;
			tif.tif_decodestrip=DumpModeDecode;
			tif.tif_decodetile=DumpModeDecode;
			tif.tif_encoderow=DumpModeEncode;
			tif.tif_encodestrip=DumpModeEncode;
			tif.tif_encodetile=DumpModeEncode;
			tif.tif_seek=DumpModeSeek;

			return true;
		}
	}
}
