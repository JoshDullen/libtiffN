#if NEXT_SUPPORT
// tif_next.cs
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
// NeXT 2-bit Grey Scale Compression Algorithm Support

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		const int NeXT_LITERALROW=0x00;
		const int NeXT_LITERALSPAN=0x40;

		static bool NeXTDecode(TIFF tif, byte[] buf, int occ, ushort s)
		{
			// Each scanline is assumed to start off as all
			// white (we assume a PhotometricInterpretation
			// of "min-is-black").
			int cc_=occ;
			for(uint op=0; (cc_--)>0; op++) buf[op]=0xff;

			uint bp=tif.tif_rawcp;
			uint cc=tif.tif_rawcc;
			uint scanline=tif.tif_scanlinesize;
			for(uint row=0; occ>0; occ-=(int)scanline, row+=scanline)
			{
				int n=tif.tif_rawdata[bp++];
				cc--;
				switch(n)
				{
					case NeXT_LITERALROW:
						// The entire scanline is given as literal values.
						if(cc<scanline) goto bad;

						Array.Copy(tif.tif_rawdata, bp, buf, row, scanline);

						bp+=scanline;
						cc-=scanline;
						break;
					case NeXT_LITERALSPAN:
						{
							// The scanline has a literal span
							// that begins at some offset.
							int off=(tif.tif_rawdata[bp]*256)+tif.tif_rawdata[bp+1];
							n=(tif.tif_rawdata[bp+2]*256)+tif.tif_rawdata[bp+3];
							if(cc<4+n||off+n>scanline) goto bad;

							Array.Copy(tif.tif_rawdata, bp+4, buf, row+off, n);

							bp+=(uint)(4+n);
							cc-=(uint)(4+n);
							break;
						}
					default:
						{
							int npixels=0, grey;
							uint imagewidth=tif.tif_dir.td_imagewidth;

							// The scanline is composed of a sequence
							// of constant color "runs". We shift
							// into "run mode" and interpret bytes
							// as codes of the form <color><npixels>
							// until we've filled the scanline.
							uint op=row;
							for(; ; )
							{
								grey=(n>>6)&0x3;
								n&=0x3f;
								while((n--)>0&&npixels<imagewidth)
								{
									switch(npixels++&3)
									{
										case 0: buf[op]=(byte)(grey<<6); break;
										case 1: buf[op]|=(byte)(grey<<4); break;
										case 2: buf[op]|=(byte)(grey<<2); break;
										case 3: buf[op++]|=(byte)grey; break;
									}
								}

								if(npixels>=imagewidth) break;
								if(cc==0) goto bad;
								n=tif.tif_rawdata[bp++];
								cc--;
							}
							break;
						}
				}
			}

			tif.tif_rawcp=bp;
			tif.tif_rawcc=cc;
			return true;

bad:
			TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "NeXTDecode: Not enough data for scanline {0}", tif.tif_row);
			return false;
		}

		static bool TIFFInitNeXT(TIFF tif, COMPRESSION scheme)
		{
			tif.tif_decoderow=NeXTDecode;
			tif.tif_decodestrip=NeXTDecode;
			tif.tif_decodetile=NeXTDecode;
			return true;
		}
	}
}
#endif // NEXT_SUPPORT