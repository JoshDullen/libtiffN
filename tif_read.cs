// tif_read.cs
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
// Scanline-oriented Read Support

using System;
using System.Collections.Generic;
using System.IO;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		const uint NOSTRIP=0xffffffff;	// undefined state
		const uint NOTILE=0xffffffff;	// undefined state

		// Seek to a random row+sample in a file.
		static bool TIFFSeek(TIFF tif, byte[] buf, uint row, ushort sample)
		{
			TIFFDirectory td=tif.tif_dir;

			if(row>=td.td_imagelength)
			{	// out of range
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Row out of range, max {1}", row, td.td_imagelength);
				return false;
			}

			uint strip;
			if(td.td_planarconfig==PLANARCONFIG.SEPARATE)
			{
				if(sample>=td.td_samplesperpixel)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Sample out of range, max {1}", sample, td.td_samplesperpixel);
					return false;
				}
				strip=sample*td.td_stripsperimage+row/td.td_rowsperstrip;
			}
			else strip=row/td.td_rowsperstrip;

			if(strip!=tif.tif_curstrip)
			{	// different strip, refill
				if(!TIFFFillStrip(tif, strip)) return false;
			}
			else if(row<tif.tif_row)
			{
				// Moving backwards within the same strip: backup
				// to the start and then decode forward (below).
				//
				// NB: If you're planning on lots of random access within a
				// strip, it's better to just read and decode the entire
				// strip, and then access the decoded data in a random fashion.
				if(!TIFFStartStrip(tif, strip)) return false;
			}

			if(row!=tif.tif_row)
			{
				// Seek forward to the desired row.
				if(!tif.tif_seek(tif, row-tif.tif_row))
				{
					// if not directly seeked to, then the slow way... line by line
					for(; tif.tif_row<row; tif.tif_row++)
						tif.tif_decoderow(tif, buf, (int)tif.tif_scanlinesize, sample);
				}
				tif.tif_row=row;
			}

			return true;
		}

		public static int TIFFReadScanline(TIFF tif, byte[] buf, uint row)
		{
			return TIFFReadScanline(tif, buf, row, 0);
		}

		public static int TIFFReadScanline(TIFF tif, byte[] buf, uint row, ushort sample)
		{
			if(!TIFFCheckRead(tif, false)) return -1;

			bool e=TIFFSeek(tif, buf, row, sample);

			if(e)
			{
				// Decompress desired row into user buffer.
				e=tif.tif_decoderow(tif, buf, (int)tif.tif_scanlinesize, sample);

				// we are now poised at the beginning of the next row
				tif.tif_row=row+1;

				if(e) tif.tif_postdecode(tif, buf, 0, (int)tif.tif_scanlinesize);
			}

			return e?1:-1;
		}

		// Read a strip of data and decompress the specified
		// amount into the user-supplied buffer.
		public static int TIFFReadEncodedStrip(TIFF tif, int strip, byte[] buf, int size)
		{
			if(!TIFFCheckRead(tif, false)) return -1;

			TIFFDirectory td=tif.tif_dir;

			if(strip>=td.td_nstrips)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Strip out of range, max {1}", strip, td.td_nstrips);
				return -1;
			}
			
			// Calculate the strip size according to the number of
			// rows in the strip (check for truncated last strip on any
			// of the separations).
			uint strips_per_sep;
			if(td.td_rowsperstrip>=td.td_imagelength) strips_per_sep=1;
			else strips_per_sep=(td.td_imagelength+td.td_rowsperstrip-1)/td.td_rowsperstrip;

			uint sep_strip=(uint)strip%strips_per_sep;

			uint nrows=td.td_imagelength%td.td_rowsperstrip;

			if(sep_strip!=strips_per_sep-1||nrows==0) nrows=td.td_rowsperstrip;

			int stripsize=TIFFVStripSize(tif, nrows);
			if(size==-1) size=stripsize;
			else if(size>stripsize) size=stripsize;

			if(TIFFFillStrip(tif, (uint)strip)&&tif.tif_decodestrip(tif, buf, size, (ushort)(strip/td.td_stripsperimage)))
			{
				tif.tif_postdecode(tif, buf, 0, size);
				return (size);
			}

			return -1;
		}

		static int TIFFReadRawStrip1(TIFF tif, uint strip, byte[] buf, int size, string module)
		{
			TIFFDirectory td=tif.tif_dir;

#if DEBUG
			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)!=0) throw new Exception("(tif.tif_flags&TIF_FLAGS.TIFF.NOREADRAW)==0");
#endif

			if(!SeekOK(tif, td.td_stripoffset[strip]))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Seek error at scanline {1}, strip {2}", tif.tif_name, tif.tif_row, strip);
				return -1;
			}

			int cc=TIFFReadFile(tif, buf, size);
			if(cc!=size)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Read error at scanline {1}; got {2} bytes, expected {3}", tif.tif_name, tif.tif_row, cc, size);
				return -1;
			}

			return size;
		}

		// Read a strip of data from the file.
		public static int TIFFReadRawStrip(TIFF tif, uint strip, byte[] buf, int size)
		{
			string module="TIFFReadRawStrip";
			TIFFDirectory td=tif.tif_dir;

			if(!TIFFCheckRead(tif, false)) return -1;

			if(strip>=td.td_nstrips)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Strip out of range, max {1}", strip, td.td_nstrips);
				return -1;
			}

			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)==TIF_FLAGS.TIFF_NOREADRAW)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Compression scheme does not support access to raw uncompressed data");
				return -1;
			}

			// FIXME: butecount should have tsize_t type, but for now libtiff
			// defines tsize_t as a signed 32-bit integer and we are losing
			// ability to read arrays larger than 2^31 bytes. So we are using
			// uint32 instead of tsize_t here.
			uint bytecount=td.td_stripbytecount[strip];
			if(bytecount<=0)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Invalid strip byte count, strip {1}", bytecount, strip);
				return -1;
			}

			if(size!=-1&&size<bytecount) bytecount=(uint)size;
			return TIFFReadRawStrip1(tif, strip, buf, (int)bytecount, module);
		}

		// Read the specified strip and setup for decoding.
		// The data buffer is expanded, as necessary, to
		// hold the strip's data.
		public static bool TIFFFillStrip(TIFF tif, uint strip)
		{
			string module="TIFFFillStrip";
			TIFFDirectory td=tif.tif_dir;

			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)==0)
			{
				// FIXME: butecount should have tsize_t type, but for now
				// libtiff defines tsize_t as a signed 32-bit integer and we
				// are losing ability to read arrays larger than 2^31 bytes.
				// So we are using uint32 instead of tsize_t here.
				uint bytecount=td.td_stripbytecount[strip];

				if(bytecount<=0)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Invalid strip byte count, strip {1}", bytecount, strip);
					return false;
				}

				// Expand raw data buffer, if needed, to
				// hold data strip coming from file
				// (perhaps should set upper bound on
				// the size of a buffer we'll use?).
				if(bytecount>tif.tif_rawdatasize)
				{
					tif.tif_curstrip=NOSTRIP;
					if((tif.tif_flags&TIF_FLAGS.TIFF_MYBUFFER)==0)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "{0}: Data buffer too small to hold strip {1}", tif.tif_name, strip);
						return false;
					}

					if(!TIFFReadBufferSetup(tif, null, (int)TIFFroundup(bytecount, 1024))) return false;
				}
				if((uint)TIFFReadRawStrip1(tif, strip, tif.tif_rawdata, (int)bytecount, module)!=bytecount) return false;

				if(!isFillOrder(tif, td.td_fillorder)&&(tif.tif_flags&TIF_FLAGS.TIFF_NOBITREV)==0)
					TIFFReverseBits(tif.tif_rawdata, (uint)bytecount);
			}
			return TIFFStartStrip(tif, strip);
		}

		// Tile-oriented Read Support
		// Contributed by Nancy Cam (Silicon Graphics).

		// Read and decompress a tile of data. The
		// tile is selected by the (x,y,z,s) coordinates.
		public static int TIFFReadTile(TIFF tif, byte[] buf, uint x, uint y, uint z, ushort s)
		{
			if(!TIFFCheckRead(tif, true)||!TIFFCheckTile(tif, x, y, z, s)) return -1;
			return TIFFReadEncodedTile(tif, TIFFComputeTile(tif, x, y, z, s), buf, -1);
		}

		// Read a tile of data and decompress the specified
		// amount into the user-supplied buffer.
		public static int TIFFReadEncodedTile(TIFF tif, uint tile, byte[] buf, int size)
		{
			TIFFDirectory td=tif.tif_dir;
			int tilesize=tif.tif_tilesize;

			if(!TIFFCheckRead(tif, true)) return -1;

			if(tile>=td.td_nstrips)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Tile out of range, max {1}", tile, td.td_nstrips);
				return -1;
			}

			if(size==-1) size=tilesize;
			else if(size>tilesize) size=tilesize;

			if(TIFFFillTile(tif, tile)&&tif.tif_decodetile(tif, buf, size, (ushort)(tile/td.td_stripsperimage)))
			{
				tif.tif_postdecode(tif, buf, 0, size);
				return size;
			}
			
			return -1;
		}

		static int TIFFReadRawTile1(TIFF tif, uint tile, byte[] buf, int size, string module)
		{
			TIFFDirectory td=tif.tif_dir;

#if DEBUG
			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)!=0) throw new Exception("(tif.tif_flags&TIF_FLAGS.TIFF.NOREADRAW)==0");
#endif

			if(!SeekOK(tif, td.td_stripoffset[tile]))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Seek error at row {1}, col {2}, tile {3}", tif.tif_name, tif.tif_row, tif.tif_col, tile);
				return -1;
			}

			int cc=TIFFReadFile(tif, buf, size);
			if(cc!=size)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Read error at row {1}, col {2}; got {3} bytes, expected {4}", tif.tif_name, tif.tif_row, tif.tif_col, cc, size);
				return -1;
			}

			return size;
		}

		// Read a tile of data from the file.
		public static int TIFFReadRawTile(TIFF tif, uint tile, byte[] buf, int size)
		{
			string module="TIFFReadRawTile";
			TIFFDirectory td=tif.tif_dir;

			if(!TIFFCheckRead(tif, true)) return -1;

			if(tile>=td.td_nstrips)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Tile out of range, max {1}", tile, td.td_nstrips);
				return -1;
			}

			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)==TIF_FLAGS.TIFF_NOREADRAW)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Compression scheme does not support access to raw uncompressed data");
				return -1;
			}

			// FIXME: butecount should have tsize_t type, but for now libtiff
			// defines tsize_t as a signed 32-bit integer and we are losing
			// ability to read arrays larger than 2^31 bytes. So we are using
			// uint32 instead of tsize_t here.
			uint bytecount=td.td_stripbytecount[tile];
			if(size!=-1&&(uint)size<bytecount) bytecount=(uint)size;

			return TIFFReadRawTile1(tif, tile, buf, (int)bytecount, module);
		}

		// Read the specified tile and setup for decoding.
		// The data buffer is expanded, as necessary, to
		// hold the tile's data.
		public static bool TIFFFillTile(TIFF tif, uint tile)
		{
			string module="TIFFFillTile";
			TIFFDirectory td=tif.tif_dir;

			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)==0)
			{
				// FIXME: bytecount should have tsize_t type, but for now libtiff
				// defines tsize_t as a signed 32-bit integer and we are losing
				// ability to read arrays larger than 2^31 bytes. So we are using
				// uint32 instead of tsize_t here.
				uint bytecount=td.td_stripbytecount[tile];
				if(bytecount<=0)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Invalid tile byte count, tile {1}", bytecount, tile);
					return false;
				}

				// Expand raw data buffer, if needed, to
				// hold data tile coming from file
				// (perhaps should set upper bound on
				// the size of a buffer we'll use?).
				if(bytecount>tif.tif_rawdatasize)
				{
					tif.tif_curtile=NOTILE;
					if((tif.tif_flags&TIF_FLAGS.TIFF_MYBUFFER)==0)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "{0}: Data buffer too small to hold tile {1}", tif.tif_name, tile);
						return false;
					}

					if(!TIFFReadBufferSetup(tif, null, (int)TIFFroundup(bytecount, 1024))) return false;
				}

				if((uint)TIFFReadRawTile1(tif, tile, tif.tif_rawdata, (int)bytecount, module)!=bytecount) return false;

				if(!isFillOrder(tif, td.td_fillorder)&&(tif.tif_flags&TIF_FLAGS.TIFF_NOBITREV)==0)
					TIFFReverseBits(tif.tif_rawdata, 0, (uint)bytecount);
			}

			return TIFFStartTile(tif, tile);
		}
		
		// Setup the raw data buffer in preparation for
		// reading a strip of raw data. If the buffer
		// is specified as zero, then a buffer of appropriate
		// size is allocated by the library. Otherwise,
		// the client must guarantee that the buffer is
		// large enough to hold any individual strip of
		// raw data.
		public static bool TIFFReadBufferSetup(TIFF tif, byte[] bp, int size)
		{
			string module="TIFFReadBufferSetup";

#if DEBUG
			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)!=0) throw new Exception("(tif.tif_flags&TIF_FLAGS.TIFF.NOREADRAW)==0");
#endif

			tif.tif_rawdata=null;

			if(bp!=null)
			{
				tif.tif_rawdatasize=(uint)size;
				tif.tif_rawdata=bp;
				tif.tif_flags&=~TIF_FLAGS.TIFF_MYBUFFER;
			}
			else
			{
				tif.tif_rawdatasize=TIFFroundup((uint)size, 1024);
				if(tif.tif_rawdatasize>0) tif.tif_rawdata=new byte[tif.tif_rawdatasize];
				tif.tif_flags|=TIF_FLAGS.TIFF_MYBUFFER;
			}

			if(tif.tif_rawdata==null||tif.tif_rawdatasize==0)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: No space for data buffer at scanline {1}", tif.tif_name, tif.tif_row);
				tif.tif_rawdatasize=0;
				return false;
			}

			return true;
		}

		// Set state to appear as if a
		// strip has just been read in.
		static bool TIFFStartStrip(TIFF tif, uint strip)
		{
			TIFFDirectory td=tif.tif_dir;

			if((tif.tif_flags&TIF_FLAGS.TIFF_CODERSETUP)==0)
			{
				if(!tif.tif_setupdecode(tif)) return false;
				tif.tif_flags|=TIF_FLAGS.TIFF_CODERSETUP;
			}
			tif.tif_curstrip=strip;
			tif.tif_row=(strip%td.td_stripsperimage)*td.td_rowsperstrip;
			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)==TIF_FLAGS.TIFF_NOREADRAW)
			{
				tif.tif_rawdata=null; // ?????
				tif.tif_rawcp=0;
				tif.tif_rawcc=0;
			}
			else
			{
				tif.tif_rawcp=0; //was tif.tif_rawdata;
				tif.tif_rawcc=td.td_stripbytecount[strip];
			}
			return tif.tif_predecode(tif, (ushort)(strip/td.td_stripsperimage));
		}

		// Set state to appear as if a
		// tile has just been read in.
		static bool TIFFStartTile(TIFF tif, uint tile)
		{
			TIFFDirectory td=tif.tif_dir;

			if((tif.tif_flags&TIF_FLAGS.TIFF_CODERSETUP)==0)
			{
				if(!tif.tif_setupdecode(tif)) return false;
				tif.tif_flags|=TIF_FLAGS.TIFF_CODERSETUP;
			}

			tif.tif_curtile=tile;
			tif.tif_row=(tile%TIFFhowmany(td.td_imagewidth, td.td_tilewidth))*td.td_tilelength;
			tif.tif_col=(tile%TIFFhowmany(td.td_imagelength, td.td_tilelength))*td.td_tilewidth;
			if((tif.tif_flags&TIF_FLAGS.TIFF_NOREADRAW)==TIF_FLAGS.TIFF_NOREADRAW)
			{
				tif.tif_rawdata=null; // ?????
				tif.tif_rawcp=0;
				tif.tif_rawcc=0;
			}
			else
			{
				tif.tif_rawcp=0; //was tif.tif_rawdata;
				tif.tif_rawcc=td.td_stripbytecount[tile];
			}

			return tif.tif_predecode(tif, (ushort)(tile/td.td_stripsperimage));
		}

		static bool TIFFCheckRead(TIFF tif, bool tiles)
		{
			if(tif.tif_mode==O.WRONLY)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "File not open for reading");
				return false;
			}

			if(tiles^isTiled(tif))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, tiles?"Can not read tiles from a stripped image":"Can not read scanlines from a tiled image");
				return false;
			}

			return true;
		}

		static void TIFFNoPostDecode(TIFF tif, byte[] buf, int buf_offset, int cc)
		{
		}

		static void TIFFSwab16BitData(TIFF tif, byte[] buf, int buf_offset, int cc)
		{
#if DEBUG
			if((cc&1)!=0) throw new Exception("cc&1!=0");
#endif
			TIFFSwabArrayOfShort(buf, buf_offset, (uint)(cc/2));
		}

		static void TIFFSwab24BitData(TIFF tif, byte[] buf, int buf_offset, int cc)
		{
#if DEBUG
			if((cc&3)!=0) throw new Exception("cc&3!=0");
#endif
			TIFFSwabArrayOfTriples(buf, buf_offset, (uint)(cc/3));
		}

		static void TIFFSwab32BitData(TIFF tif, byte[] buf, int buf_offset, int cc)
		{
#if DEBUG
			if((cc&3)!=0) throw new Exception("cc&3!=0");
#endif
			TIFFSwabArrayOfLong(buf, buf_offset, (uint)(cc/4));
		}

		static void TIFFSwab64BitData(TIFF tif, byte[] buf, int buf_offset, int cc)
		{
#if DEBUG
			if((cc&7)!=0) throw new Exception("cc&7!=0");
#endif
			TIFFSwabArrayOfDouble(buf, buf_offset, (uint)(cc/8));
		}
	}
}
