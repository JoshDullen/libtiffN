// tif_tile.cs
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
// Tiled Image Support Routines.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// Compute which tile an (x,y,z,s) value is in.
		public static uint TIFFComputeTile(TIFF tif, uint x, uint y, uint z, ushort s)
		{
			TIFFDirectory td=tif.tif_dir;
			uint dx=td.td_tilewidth;
			uint dy=td.td_tilelength;
			uint dz=td.td_tiledepth;

			if(td.td_imagedepth==1) z=0;
			if(dx==0xffffffff) dx=td.td_imagewidth;
			if(dy==0xffffffff) dy=td.td_imagelength;
			if(dz==0xffffffff) dz=td.td_imagedepth;

			if(dx!=0&&dy!=0&&dz!=0)
			{
				uint xpt=TIFFhowmany(td.td_imagewidth, dx);
				uint ypt=TIFFhowmany(td.td_imagelength, dy);
				uint zpt=TIFFhowmany(td.td_imagedepth, dz);

				if(td.td_planarconfig==PLANARCONFIG.SEPARATE)
					return xpt*ypt*zpt*s+xpt*ypt*(z/dz)+xpt*(y/dy)+x/dx;

				return xpt*ypt*(z/dz)+xpt*(y/dy)+x/dx;
			}

			return 1;
		}

		// Check an (x,y,z,s) coordinate against the image bounds.
		public static bool TIFFCheckTile(TIFF tif, uint x, uint y, uint z, ushort s)
		{
			TIFFDirectory td=tif.tif_dir;

			if(x>=td.td_imagewidth)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Col out of range, max {1}", x, td.td_imagewidth-1);
				return false;
			}

			if(y>=td.td_imagelength)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Row out of range, max {1}", y, td.td_imagelength-1);
				return false;
			}

			if(z>=td.td_imagedepth)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Depth out of range, max {1}", z, td.td_imagedepth-1);
				return false;
			}

			if(td.td_planarconfig==PLANARCONFIG.SEPARATE&&s>=td.td_samplesperpixel)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Sample out of range, max {1}", s, td.td_samplesperpixel-1);
				return false;
			}

			return true;
		}

		// Compute how many tiles are in an image.
		public static uint TIFFNumberOfTiles(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint dx=td.td_tilewidth;
			uint dy=td.td_tilelength;
			uint dz=td.td_tiledepth;
			uint ntiles;

			if(dx==0xffffffff) dx=td.td_imagewidth;
			if(dy==0xffffffff) dy=td.td_imagelength;
			if(dz==0xffffffff) dz=td.td_imagedepth;

			ntiles=(dx==0||dy==0||dz==0)?0:
				multiply(tif,
					multiply(tif, TIFFhowmany(td.td_imagewidth, dx), TIFFhowmany(td.td_imagelength, dy), "TIFFNumberOfTiles"),
					TIFFhowmany(td.td_imagedepth, dz),
				"TIFFNumberOfTiles");

			if(td.td_planarconfig==PLANARCONFIG.SEPARATE) ntiles=multiply(tif, ntiles, td.td_samplesperpixel, "TIFFNumberOfTiles");
			return ntiles;
		}

		// Compute the # bytes in each row of a tile.
		public static int TIFFTileRowSize(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint rowsize;

			if(td.td_tilelength==0||td.td_tilewidth==0) return 0;
			rowsize=multiply(tif, td.td_bitspersample, td.td_tilewidth, "TIFFTileRowSize");
			if(td.td_planarconfig==PLANARCONFIG.CONTIG) rowsize=multiply(tif, rowsize, td.td_samplesperpixel, "TIFFTileRowSize");
			return (int)TIFFhowmany8(rowsize);
		}

		// Compute the # bytes in a variable length, row-aligned tile.
		public static int TIFFVTileSize(TIFF tif, uint nrows)
		{
			TIFFDirectory td=tif.tif_dir;
			uint tilesize;

			if(td.td_tilelength==0||td.td_tilewidth==0||td.td_tiledepth==0) return 0;

			if(td.td_planarconfig==PLANARCONFIG.CONTIG&&td.td_photometric==PHOTOMETRIC.YCBCR&&!isUpSampled(tif))
			{
				// Packed YCbCr data contain one Cb+Cr for every
				// HorizontalSampling*VerticalSampling Y values.
				// Must also roundup width and height when calculating
				// since images that are not a multiple of the
				// horizontal/vertical subsampling area include
				// YCbCr data for the extended image.
				uint w=TIFFroundup(td.td_tilewidth, td.td_ycbcrsubsampling[0]);
				uint rowsize=TIFFhowmany8(multiply(tif, w, td.td_bitspersample, "TIFFVTileSize"));
				uint samplingarea=(uint)td.td_ycbcrsubsampling[0]*td.td_ycbcrsubsampling[1];

				if(samplingarea==0)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Invalid YCbCr subsampling");
					return 0;
				}

				nrows=TIFFroundup(nrows, td.td_ycbcrsubsampling[1]);

				// NB: don't need TIFFhowmany here 'cuz everything is rounded
				tilesize=multiply(tif, nrows, rowsize, "TIFFVTileSize");
				tilesize=summarize(tif, tilesize, multiply(tif, 2, tilesize/samplingarea, "TIFFVTileSize"), "TIFFVTileSize");
			}
			else tilesize=multiply(tif, nrows, (uint)TIFFTileRowSize(tif), "TIFFVTileSize");

			return (int)multiply(tif, tilesize, td.td_tiledepth, "TIFFVTileSize");
		}

		// Compute the # bytes in a row-aligned tile.
		public static int TIFFTileSize(TIFF tif)
		{
			return TIFFVTileSize(tif, tif.tif_dir.td_tilelength);
		}

		// Compute a default tile size based on the image
		// characteristics and a requested value. If a
		// request is <1 then we choose a size according
		// to certain heuristics.
		public static void TIFFDefaultTileSize(TIFF tif, ref uint tw, ref uint th)
		{
			tif.tif_deftilesize(tif, ref tw, ref th);
		}

		static void _TIFFDefaultTileSize(TIFF tif, ref uint tw, ref uint th)
		{
			if((int)tw<1) tw=256;
			if((int)th<1) th=256;

			// roundup to a multiple of 16 per the spec
			if((tw&0xf)!=0) tw=TIFFroundup(tw, 16);
			if((th&0xf)!=0) th=TIFFroundup(th, 16);
		}
	}
}
