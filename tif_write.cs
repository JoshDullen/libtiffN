// tif_write.cs
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
// Scanline-oriented Write Support

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		static bool BUFFERCHECK(TIFF tif)
		{
			return ((tif.tif_flags&TIF_FLAGS.TIFF_BUFFERSETUP)!=0&&tif.tif_rawdata!=null)||TIFFWriteBufferSetup((tif), null, -1);
		}

		public static int TIFFWriteScanline(TIFF tif, byte[] buf, uint row, ushort sample)
		{
			string module="TIFFWriteScanline";

			if(!((tif.tif_flags&TIF_FLAGS.TIFF_BEENWRITING)!=0||TIFFWriteCheck(tif, false, module))) return -1;

			// Handle delayed allocation of data buffer. This
			// permits it to be sized more intelligently (using
			// directory information).
			if(!BUFFERCHECK(tif)) return -1;
			TIFFDirectory td=tif.tif_dir;
			bool imagegrew=false;

			// Extend image length if needed
			// (but only for PlanarConfig=1).
			if(row>=td.td_imagelength)
			{	// extend image
				if(td.td_planarconfig==PLANARCONFIG.SEPARATE)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Can not change \"ImageLength\" when using separate planes");
					return -1;
				}

				td.td_imagelength=row+1;
				imagegrew=true;
			}

			// Calculate strip and check for crossings.
			uint strip;
			if(td.td_planarconfig==PLANARCONFIG.SEPARATE)
			{
				if(sample>=td.td_samplesperpixel)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Sample out of range, max {1}", sample, td.td_samplesperpixel);
					return -1;
				}
				strip=sample*td.td_stripsperimage+row/td.td_rowsperstrip;
			}
			else strip=row/td.td_rowsperstrip;

			// Check strip array to make sure there's space. We don't support
			// dynamically growing files that have data organized in separate
			// bitplanes because it's too painful. In that case we require that
			// the imagelength be set properly before the first write (so that the
			// strips array will be fully allocated above).
			if(strip>=td.td_nstrips&&!TIFFGrowStrips(tif, 1, module)) return -1;

			if(strip!=tif.tif_curstrip)
			{
				// Changing strips -- flush any data present.
				if(!TIFFFlushData(tif)) return -1;

				tif.tif_curstrip=strip;
				
				// Watch out for a growing image. The value of strips/image
				// will initially be 1 (since it can't be deduced until the
				// imagelength is known).
				if(strip>=td.td_stripsperimage&&imagegrew) td.td_stripsperimage=TIFFhowmany(td.td_imagelength, td.td_rowsperstrip);

				tif.tif_row=(strip%td.td_stripsperimage)*td.td_rowsperstrip;
				if((tif.tif_flags&TIF_FLAGS.TIFF_CODERSETUP)==0)
				{
					if(!tif.tif_setupencode(tif)) return -1;
					tif.tif_flags|=TIF_FLAGS.TIFF_CODERSETUP;
				}

				tif.tif_rawcc=0;
				tif.tif_rawcp=0;

				if(td.td_stripbytecount[strip]>0)
				{
					// Force TIFFAppendToStrip() to consider placing data at end of file.
					tif.tif_curoff=0;
				}

				if(!tif.tif_preencode(tif, sample)) return -1;
				tif.tif_flags|=TIF_FLAGS.TIFF_POSTENCODE;
			}
			
			// Ensure the write is either sequential or at the
			// beginning of a strip (or that we can randomly
			// access the data -- i.e. no encoding).
			if(row!=tif.tif_row)
			{
				if(row<tif.tif_row)
				{
					// Moving backwards within the same strip:
					// backup to the start and then decode
					// forward (below).
					tif.tif_row=(strip%td.td_stripsperimage)*td.td_rowsperstrip;
					tif.tif_rawcp=0;
				}

				// Seek forward to the desired row.
				if(!tif.tif_seek(tif, row-tif.tif_row)) return -1;
				tif.tif_row=row;
			}

			// swab if needed - note that source buffer will be altered
			tif.tif_postdecode(tif, buf, 0, (int)tif.tif_scanlinesize);

			bool status=tif.tif_encoderow(tif, buf, (int)tif.tif_scanlinesize, sample);

			// we are now poised at the beginning of the next row
			tif.tif_row=row+1;

			return status?1:0;
		}

		// Encode the supplied data and write it to the
		// specified strip.
		//
		// NB: Image length must be setup before writing.
		public static int TIFFWriteEncodedStrip(TIFF tif, uint strip, byte[] data, int cc)
		{
			string module="TIFFWriteEncodedStrip";
			TIFFDirectory td=tif.tif_dir;
			ushort sample;

			if(!((tif.tif_flags&TIF_FLAGS.TIFF_BEENWRITING)!=0||TIFFWriteCheck(tif, false, module))) return -1;

			// Check strip array to make sure there's space.
			// We don't support dynamically growing files that
			// have data organized in separate bitplanes because
			// it's too painful. In that case we require that
			// the imagelength be set properly before the first
			// write (so that the strips array will be fully
			// allocated above).
			///
			if(strip>=td.td_nstrips)
			{
				if(td.td_planarconfig==PLANARCONFIG.SEPARATE)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Can not grow image by strips when using separate planes");
					return -1;
				}

				if(!TIFFGrowStrips(tif, 1, module)) return -1;
				td.td_stripsperimage=TIFFhowmany(td.td_imagelength, td.td_rowsperstrip);
			}
			
			// Handle delayed allocation of data buffer. This
			// permits it to be sized according to the directory
			// info.
			if(!BUFFERCHECK(tif)) return -1;

			tif.tif_curstrip=strip;
			tif.tif_row=(strip%td.td_stripsperimage)*td.td_rowsperstrip;
			if((tif.tif_flags&TIF_FLAGS.TIFF_CODERSETUP)==0)
			{
				if(!tif.tif_setupencode(tif)) return -1;
				tif.tif_flags|=TIF_FLAGS.TIFF_CODERSETUP;
			}

			tif.tif_rawcc=0;
			tif.tif_rawcp=0;

			if(td.td_stripbytecount[strip]>0)
			{
				// Force TIFFAppendToStrip() to consider placing data at end of file.
				tif.tif_curoff=0;
			}

			tif.tif_flags&=~TIF_FLAGS.TIFF_POSTENCODE;
			sample=(ushort)(strip/td.td_stripsperimage);
			if(!tif.tif_preencode(tif, sample)) return -1;

			// swab if needed - note that source buffer will be altered
			tif.tif_postdecode(tif, data, 0, cc);

			if(!tif.tif_encodestrip(tif, data, cc, sample)) return 0;
			if(!tif.tif_postencode(tif)) return -1;
			if(!isFillOrder(tif, td.td_fillorder)&&(tif.tif_flags&TIF_FLAGS.TIFF_NOBITREV)==0) TIFFReverseBits(tif.tif_rawdata, tif.tif_rawcc);
			if(tif.tif_rawcc>0&&!TIFFAppendToStrip(tif, strip, tif.tif_rawdata, tif.tif_rawcc)) return -1;

			tif.tif_rawcc=0;
			tif.tif_rawcp=0;

			return cc;
		}

		// Write the supplied data to the specified strip.
		//
		// NB: Image length must be setup before writing.
		public static int TIFFWriteRawStrip(TIFF tif, uint strip, byte[] data, int cc)
		{
			string module="TIFFWriteRawStrip";
			TIFFDirectory td=tif.tif_dir;

			if(!((tif.tif_flags&TIF_FLAGS.TIFF_BEENWRITING)!=0||TIFFWriteCheck(tif, false, module))) return -1;

			// Check strip array to make sure there's space.
			// We don't support dynamically growing files that
			// have data organized in separate bitplanes because
			// it's too painful. In that case we require that
			// the imagelength be set properly before the first
			// write (so that the strips array will be fully
			// allocated above).
			if(strip>=td.td_nstrips)
			{
				if(td.td_planarconfig==PLANARCONFIG.SEPARATE)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Can not grow image by strips when using separate planes");
					return -1;
				}

				// Watch out for a growing image. The value of
				// strips/image will initially be 1 (since it
				// can't be deduced until the imagelength is known).
				if(strip>=td.td_stripsperimage) td.td_stripsperimage=TIFFhowmany(td.td_imagelength, td.td_rowsperstrip);
				if(!TIFFGrowStrips(tif, 1, module)) return -1;
			}

			tif.tif_curstrip=strip;
			tif.tif_row=(strip%td.td_stripsperimage)*td.td_rowsperstrip;
			return TIFFAppendToStrip(tif, strip, data, (uint)cc)?cc:-1;
		}

		// Write and compress a tile of data. The
		// tile is selected by the (x,y,z,s) coordinates.
		public static int TIFFWriteTile(TIFF tif, byte[] buf, uint x, uint y, uint z, ushort s)
		{
			if(!TIFFCheckTile(tif, x, y, z, s)) return -1;

			// NB:	A tile size of -1 is used instead of tif_tilesize knowing
			//		that TIFFWriteEncodedTile will clamp this to the tile size.
			//		This is done because the tile size may not be defined until
			//		after the output buffer is setup in TIFFWriteBufferSetup.
			return TIFFWriteEncodedTile(tif, TIFFComputeTile(tif, x, y, z, s), buf, -1);
		}

		// Encode the supplied data and write it to the
		// specified tile. There must be space for the
		// data. The function clamps individual writes
		// to a tile to the tile size, but does not (and
		// can not) check that multiple writes to the same
		// tile do not write more than tile size data.
		//
		// NB:	Image length must be setup before writing; this
		//		interface does not support automatically growing
		//		the image on each write (as TIFFWriteScanline does).
		public static int TIFFWriteEncodedTile(TIFF tif, uint tile, byte[] data, int cc)
		{
			string module="TIFFWriteEncodedTile";
			ushort sample;

			if(!((tif.tif_flags&TIF_FLAGS.TIFF_BEENWRITING)!=0||TIFFWriteCheck(tif, true, module))) return -1;
			TIFFDirectory td=tif.tif_dir;
			if(tile>=td.td_nstrips)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Tile {1} out of range, max {2}", tif.tif_name, tile, td.td_nstrips);
				return -1;
			}

			// Handle delayed allocation of data buffer. This
			// permits it to be sized more intelligently (using
			// directory information).
			if(!BUFFERCHECK(tif)) return -1;

			tif.tif_curtile=tile;

			tif.tif_rawcc=0;
			tif.tif_rawcp=0;;

			if(td.td_stripbytecount[tile]>0)
			{
				// if we are writing over existing tiles, zero length.
				td.td_stripbytecount[tile]=0;

				// this forces TIFFAppendToStrip() to do a seek.
				tif.tif_curoff=0;
			}

			// Compute tiles per row & per column to compute
			// current row and column
			tif.tif_row=(tile%TIFFhowmany(td.td_imagelength, td.td_tilelength))*td.td_tilelength;
			tif.tif_col=(tile%TIFFhowmany(td.td_imagewidth, td.td_tilewidth))*td.td_tilewidth;

			if((tif.tif_flags&TIF_FLAGS.TIFF_CODERSETUP)==0)
			{
				if(!tif.tif_setupencode(tif)) return -1;
				tif.tif_flags|=TIF_FLAGS.TIFF_CODERSETUP;
			}

			tif.tif_flags&=~TIF_FLAGS.TIFF_POSTENCODE;
			sample=(ushort)(tile/td.td_stripsperimage);
			if(!tif.tif_preencode(tif, sample)) return -1;

			// Clamp write amount to the tile size. This is mostly
			// done so that callers can pass in some large number
			// (e.g. -1) and have the tile size used instead.
			if(cc<1||cc>tif.tif_tilesize) cc=tif.tif_tilesize;

			// swab if needed - note that source buffer will be altered
			tif.tif_postdecode(tif, data, 0, cc);

			if(!tif.tif_encodetile(tif, data, cc, sample)) return 0;
			if(!tif.tif_postencode(tif)) return -1;

			if(!isFillOrder(tif, td.td_fillorder)&&(tif.tif_flags&TIF_FLAGS.TIFF_NOBITREV)==0) TIFFReverseBits(tif.tif_rawdata, tif.tif_rawcc);
			
			if(tif.tif_rawcc>0&&!TIFFAppendToStrip(tif, tile, tif.tif_rawdata, tif.tif_rawcc)) return -1;

			tif.tif_rawcc=0;
			tif.tif_rawcp=0;

			return cc;
		}

		// Write the supplied data to the specified strip.
		// There must be space for the data; we don't check
		// if strips overlap!
		//
		// NB:	Image length must be setup before writing; this
		//		interface does not support automatically growing
		//		the image on each write (as TIFFWriteScanline does).
		public static int TIFFWriteRawTile(TIFF tif, uint tile, byte[] data, int cc)
		{
			string module="TIFFWriteRawTile";

			if(!((tif.tif_flags&TIF_FLAGS.TIFF_BEENWRITING)!=0||TIFFWriteCheck(tif, true, module))) return -1;
			if(tile>=tif.tif_dir.td_nstrips)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Tile {1} out of range, max {2}", tif.tif_name, tile, tif.tif_dir.td_nstrips);
				return -1;
			}
			return TIFFAppendToStrip(tif, tile, data, (uint)cc)?cc:-1;
		}

		static bool isUnspecified(TIFF tif, FIELD f)
		{
			return TIFFFieldSet(tif, f)&&tif.tif_dir.td_imagelength==0;
		}

		public static bool TIFFSetupStrips(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;

			if(isTiled(tif)) td.td_stripsperimage=isUnspecified(tif, FIELD.TILEDIMENSIONS)?td.td_samplesperpixel:TIFFNumberOfTiles(tif);
			else td.td_stripsperimage=isUnspecified(tif, FIELD.ROWSPERSTRIP)?td.td_samplesperpixel:(uint)TIFFNumberOfStrips(tif);

			td.td_nstrips=td.td_stripsperimage;
			if(td.td_planarconfig==PLANARCONFIG.SEPARATE) td.td_stripsperimage/=td.td_samplesperpixel;

			try
			{
				td.td_stripoffset=new uint[td.td_nstrips];
				td.td_stripbytecount=new uint[td.td_nstrips];
			}
			catch
			{
				return false;
			}

			// Place data at the end-of-file
			// (by setting offsets to zero).
			TIFFSetFieldBit(tif, FIELD.STRIPOFFSETS);
			TIFFSetFieldBit(tif, FIELD.STRIPBYTECOUNTS);

			// FIX: Some tools don't like images without ROWSPERSTRIP set.
			if(!TIFFFieldSet(tif, FIELD.ROWSPERSTRIP))
			{
				td.td_rowsperstrip=td.td_imagelength;
				TIFFSetFieldBit(tif, FIELD.ROWSPERSTRIP);
			}

			return true;
		}

		// Verify file is writable and that the directory
		// information is setup properly. In doing the latter
		// we also "freeze" the state of the directory so
		// that important information is not changed.
		public static bool TIFFWriteCheck(TIFF tif, bool tiles, string module)
		{
			if(tif.tif_mode==O.RDONLY)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: File not open for writing", tif.tif_name);
				return false;
			}
			if(tiles^isTiled(tif))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, tiles?"Can not write tiles to a stripped image":"Can not write scanlines to a tiled image");
				return false;
			}

			// On the first write verify all the required information
			// has been setup and initialize any data structures that
			// had to wait until directory information was set.
			// Note that a lot of our work is assumed to remain valid
			// because we disallow any of the important parameters
			// from changing after we start writing (i.e. once
			// TIFF_BEENWRITING is set, TIFFSetField will only allow
			// the image's length to be changed).
			if(!TIFFFieldSet(tif, FIELD.IMAGEDIMENSIONS))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Must set \"ImageWidth\" before writing data", tif.tif_name);
				return false;
			}

			if(tif.tif_dir.td_samplesperpixel==1)
			{
				// Planarconfiguration is irrelevant in case of single band
				// images and need not be included. We will set it anyway,
				// because this field is used in other parts of library even
				// in the single band case.
				if(!TIFFFieldSet(tif, FIELD.PLANARCONFIG))
					tif.tif_dir.td_planarconfig=PLANARCONFIG.CONTIG;
			}
			else
			{
				if(!TIFFFieldSet(tif, FIELD.PLANARCONFIG))
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: Must set \"PlanarConfiguration\" before writing data", tif.tif_name);
					return false;
				}
			}
			if(tif.tif_dir.td_stripoffset==null&&!TIFFSetupStrips(tif))
			{
				tif.tif_dir.td_nstrips=0;
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: No space for {1} arrays", tif.tif_name, isTiled(tif)?"tile":"strip");
				return false;
			}
			tif.tif_tilesize=isTiled(tif)?TIFFTileSize(tif):-1;
			tif.tif_scanlinesize=(uint)TIFFScanlineSize(tif);
			tif.tif_flags|=TIF_FLAGS.TIFF_BEENWRITING;
			return true;
		}

		// Setup the raw data buffer used for encoding.
		public static bool TIFFWriteBufferSetup(TIFF tif, byte[] bp, int size)
		{
			string module="TIFFWriteBufferSetup";

			tif.tif_rawdata=null;

			if(size==-1)
			{
				size=(isTiled(tif)?tif.tif_tilesize:TIFFStripSize(tif));
				// Make raw data buffer at least 8K
				if(size<8*1024) size=8*1024;
				bp=null; // NB: force new allocation
			}

			if(bp==null)
			{
				try
				{
					bp=new byte[size];
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: No space for output buffer", tif.tif_name);
					return false;
				}
			}

			tif.tif_rawdata=bp;
			tif.tif_rawdatasize=(uint)size;
			tif.tif_rawcc=0;
			tif.tif_rawcp=0;
			tif.tif_flags|=TIF_FLAGS.TIFF_BUFFERSETUP;

			return true;
		}

		// Grow the strip data structures by delta strips.
		static bool TIFFGrowStrips(TIFF tif, int delta, string module)
		{
			TIFFDirectory td=tif.tif_dir;
			uint[] new_stripoffset=null, new_stripbytecount=null;

#if DEBUG
			if(td.td_planarconfig!=PLANARCONFIG.CONTIG) throw new Exception("td.td_planarconfig!=PLANARCONFIG.CONTIG");
#endif
			if(delta==0) return true;

			try
			{
				new_stripoffset=new uint[td.td_nstrips+delta];
				new_stripbytecount=new uint[td.td_nstrips+delta];
				if(delta>0)
				{
					if(td.td_stripoffset!=null) td.td_stripoffset.CopyTo(new_stripoffset, 0);
					if(td.td_stripbytecount!=null) td.td_stripbytecount.CopyTo(new_stripbytecount, 0);
				}
				else
				{
					if(td.td_stripoffset!=null) Array.Copy(td.td_stripoffset, new_stripoffset, td.td_nstrips+delta);
					if(td.td_stripbytecount!=null) Array.Copy(td.td_stripbytecount, new_stripbytecount, td.td_nstrips+delta);
				}
			}
			catch
			{
				td.td_nstrips=0;
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: No space to expand strip arrays", tif.tif_name);
				return false;
			}

			td.td_stripoffset=new_stripoffset;
			td.td_stripbytecount=new_stripbytecount;

			td.td_nstrips=(uint)(td.td_nstrips+delta);
			return true;
		}

		// Append the data to the specified strip.
		static bool TIFFAppendToStrip(TIFF tif, uint strip, byte[] data, uint cc)
		{
			string module="TIFFAppendToStrip";
			TIFFDirectory td=tif.tif_dir;

			if(td.td_stripoffset[strip]==0||tif.tif_curoff==0)
			{
#if DEBUG
				if(td.td_nstrips<=0) throw new Exception("td.td_nstrips>0");
#endif

				if(td.td_stripbytecount[strip]!=0&&td.td_stripoffset[strip]!=0&&td.td_stripbytecount[strip]>=cc)
				{
					// There is already tile data on disk, and the new tile
					// data we have to will fit in the same space. The only 
					// aspect of this that is risky is that there could be
					// more data to append to this strip before we are done
					// depending on how we are getting called.
					if(!SeekOK(tif, td.td_stripoffset[strip]))
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Seek error at scanline {0}", tif.tif_row);
						return false;
					}
				}
				else
				{
					// Seek to end of file, and set that as our location to write this strip.
					td.td_stripoffset[strip] = TIFFSeekFile(tif, 0, SEEK.END);
				}

				//    if(td.td_stripbytecountsorted!=0)
				//    {
				//        if(strip==td.td_nstrips-1||td.td_stripoffset[strip+1]<td.td_stripoffset[strip]+cc)
				//        {
				//            td.td_stripoffset[strip]=TIFFSeekFile(tif, 0, SEEK.END);
				//        }
				//    }
				//    else
				//    {
				//        for(uint i=0; i<td.td_nstrips; i++)
				//        {
				//            if(td.td_stripoffset[i]>td.td_stripoffset[strip]&&td.td_stripoffset[i]<td.td_stripoffset[strip]+cc)
				//            {
				//                td.td_stripoffset[strip]=TIFFSeekFile(tif, 0, SEEK.END);
				//            }
				//        }
				//    }

				//    if(!SeekOK(tif, td.td_stripoffset[strip]))
				//    {
				//        TIFFErrorExt(tif.tif_clientdata, module, "{0}: Seek error at scanline {1}", tif.tif_name, tif.tif_row);
				//        return false;
				//    }
				//}
				//else td.td_stripoffset[strip]=TIFFSeekFile(tif, 0, SEEK.END);
				tif.tif_curoff=td.td_stripoffset[strip];

				// We are starting a fresh strip/tile, so set the size to zero.
				td.td_stripbytecount[strip]=0;
			}

			if(!WriteOK(tif, data, (int)cc))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Write error at scanline {1}", tif.tif_row);
				return false;
			}

			tif.tif_curoff+=cc;
			td.td_stripbytecount[strip]+=cc;
			return true;
		}

		// Internal version of TIFFFlushData that can be
		// called by "encodestrip routines" w/o concern
		// for infinite recursion.
		static bool TIFFFlushData1(TIFF tif)
		{
			if(tif.tif_rawcc>0)
			{
				if(!isFillOrder(tif, (TIF_FLAGS)tif.tif_dir.td_fillorder)&&(tif.tif_flags&TIF_FLAGS.TIFF_NOBITREV)==0) TIFFReverseBits(tif.tif_rawdata, tif.tif_rawcc);
				if(!TIFFAppendToStrip(tif, isTiled(tif)?tif.tif_curtile:tif.tif_curstrip, tif.tif_rawdata, tif.tif_rawcc)) return false;
				tif.tif_rawcc=0;
				tif.tif_rawcp=0;
			}
			return true;
		}

		// Set the current write offset. This should only be
		// used to set the offset to a known previous location
		// (very carefully), or to 0 so that the next write gets
		// appended to the end of the file.
		public static void TIFFSetWriteOffset(TIFF tif, uint off)
		{
			tif.tif_curoff=off;
		}
	}
}
