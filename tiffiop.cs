// tiffiop.cs
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
using System.IO;

namespace Free.Ports.LibTiff
{
	// Dummy interface for not using 'object' for TIFF.tif_data
	interface ICodecState
	{
	}

	public static partial class libtiff
	{
		const int STRIP_SIZE_DEFAULT=8192;
	}

	class TIFFClientInfoLink
	{
		internal object data;
		internal string name;
	}

	// Delegates for "method pointers" used internally.
	delegate void TIFFVoidMethod(TIFF tif);
	delegate bool TIFFBoolMethod(TIFF tif);
	delegate bool TIFFPreMethod(TIFF tif, ushort sampleNumber);
	delegate bool TIFFCodeMethod(TIFF tif, byte[] buffer, int cc, ushort sampleNumber);
	delegate bool TIFFSeekMethod(TIFF tif, uint nrows);
	delegate void TIFFPostMethod(TIFF tif, byte[] buffer, int buffer_offset, int cc);
	delegate uint TIFFStripMethod(TIFF tif, uint s);
	delegate void TIFFTileMethod(TIFF tif, ref uint tw, ref uint th);

	[Flags]
	enum TIF_FLAGS
	{
		FILLORDER_MSB2LSB=FILLORDER.MSB2LSB,
		FILLORDER_LSB2MSB=FILLORDER.LSB2MSB,
		TIFF_FILLORDER=FILLORDER_MSB2LSB|FILLORDER_LSB2MSB, // natural bit fill order for machine
		TIFF_DIRTYHEADER=0x00004,	// header must be written on close
		TIFF_DIRTYDIRECT=0x00008,	// current directory must be written
		TIFF_BUFFERSETUP=0x00010,	// data buffers setup
		TIFF_CODERSETUP=0x00020,	// encoder/decoder setup done
		TIFF_BEENWRITING=0x00040,	// written 1+ scanlines to file
		TIFF_SWAB=0x00080,			// byte swap file information
		TIFF_NOBITREV=0x00100,		// inhibit bit reversal logic
		TIFF_MYBUFFER=0x00200,		// my raw data buffer; free on close
		TIFF_ISTILED=0x00400,		// file is tile, not strip- based
		TIFF_MAPPED=0x00800,		// file is mapped into memory
		TIFF_POSTENCODE=0x01000,	// need call to postencode routine
		TIFF_INSUBIFD=0x02000,		// currently writing a subifd
		TIFF_UPSAMPLED=0x04000,		// library is doing data up-sampling
		TIFF_STRIPCHOP=0x08000,		// enable strip chopping support
		TIFF_HEADERONLY=0x10000,	// read header only, do not process
		TIFF_NOREADRAW=0x20000,		// skip reading of raw uncompressed image data
		TIFF_INCUSTOMIFD=0x40000,	// currently writing a custom IFD
	}

	public class TIFF
	{
		internal string tif_name;		// name of open file
		internal Stream tif_fd;			// open file descriptor
		internal O tif_mode;			// open mode (O_*)
		internal TIF_FLAGS tif_flags;

		// the first directory
		internal uint tif_diroff;		// file offset of current directory
		internal uint tif_nextdiroff;	// file offset of following directory
		internal List<uint> tif_dirlist=new List<uint>();	// list of offsets to already seen directories to prevent IFD looping

		// directories to prevent IFD looping
		//internal uint tif_dirlistsize;	// number of entires in offset list
		internal ushort tif_dirnumber;	// number of already seen directories
		internal TIFFDirectory tif_dir=new TIFFDirectory();			// internal rep of current directory
		internal TIFFDirectory tif_customdir=new TIFFDirectory();	// custom IFDs are separated from the main ones
		internal TIFFHeader tif_header=new TIFFHeader();			// file's header block
		internal uint tif_row;			// current scanline
		internal ushort tif_curdir;		// current directory (index)
		internal uint tif_curstrip;		// current strip for read/write
		internal uint tif_curoff;		// current offset for read/write
		internal uint tif_dataoff;		// current offset for writing dir

		// SubIFD support
		internal ushort tif_nsubifd;	// remaining subifds to write
		internal uint tif_subifdoff;	// offset for patching SubIFD link

		// tiling support
		internal uint tif_col;		// current column (offset by row too)
		internal uint tif_curtile;	// current tile for read/write
		internal int tif_tilesize;	// # of bytes in a tile

		// compression scheme hooks
		internal bool tif_decodestatus;
		internal TIFFBoolMethod tif_setupdecode;	// called once before predecode
		internal TIFFPreMethod tif_predecode;		// pre- row/strip/tile decoding
		internal TIFFBoolMethod tif_setupencode;	// called once before preencode

		internal bool tif_encodestatus;
		internal TIFFPreMethod tif_preencode;		// pre- row/strip/tile encoding
		internal TIFFBoolMethod tif_postencode;		// post- row/strip/tile encoding
		internal TIFFCodeMethod tif_decoderow;		// scanline decoding routine
		internal TIFFCodeMethod tif_encoderow;		// scanline encoding routine
		internal TIFFCodeMethod tif_decodestrip;	// strip decoding routine
		internal TIFFCodeMethod tif_encodestrip;	// strip encoding routine
		internal TIFFCodeMethod tif_decodetile;		// tile decoding routine
		internal TIFFCodeMethod tif_encodetile;		// tile encoding routine
		internal TIFFVoidMethod tif_close;			// cleanup-on-close routine
		internal TIFFSeekMethod tif_seek;			// position within a strip routine
		internal TIFFVoidMethod tif_cleanup;		// cleanup state routine
		internal TIFFStripMethod tif_defstripsize;	// calculate/constrain strip size
		internal TIFFTileMethod tif_deftilesize;	// calculate/constrain tile size
		internal ICodecState tif_data;				// compression scheme private data

		// input/output buffering
		internal uint tif_scanlinesize;	// # of bytes in a scanline
		internal byte[] tif_rawdata;	// raw data buffer
		internal uint tif_rawdatasize;	// # of bytes in raw data buffer
		internal uint tif_rawcp;		// current spot in raw buffer
		internal uint tif_rawcc;		// bytes unread from raw buffer

		// input/output callback methods
		internal Stream tif_clientdata;				// callback parameter
		internal TIFFReadWriteProc tif_readproc;	// read method
		internal TIFFReadWriteProc tif_writeproc;	// write method
		internal TIFFSeekProc tif_seekproc;			// lseek method
		internal TIFFCloseProc tif_closeproc;		// close method
		internal TIFFSizeProc tif_sizeproc;			// filesize method

		// post-decoding support
		internal TIFFPostMethod tif_postdecode;		// post decoding routine

		// tag support
		internal List<TIFFFieldInfo> tif_fieldinfo=new List<TIFFFieldInfo>();				// sorted table of registered tags
		//internal int tif_nfields;															// # entries in registered tag table
		internal TIFFFieldInfo tif_foundfield;												// cached pointer to already found tag
		internal TIFFTagMethods tif_tagmethods=new TIFFTagMethods();						// tag get/set/print routines
		internal List<TIFFClientInfoLink> tif_clientinfo=new List<TIFFClientInfoLink>();	// extra client information.
	}

	public static partial class libtiff
	{
		// is tag value normal or pseudo
		static bool isPseudoTag(TIFFTAG t) { return (uint)t>0xffff; }
		static bool isPseudoTag(uint t) { return t>0xffff; }

		static bool isTiled(TIFF tif) { return (tif.tif_flags&TIF_FLAGS.TIFF_ISTILED)!=0; }
		static bool isFillOrder(TIFF tif, TIF_FLAGS o) { return (tif.tif_flags&o)!=0; }
		static bool isFillOrder(TIFF tif, FILLORDER o) { return (tif.tif_flags&(TIF_FLAGS)o)!=0; }
		static bool isUpSampled(TIFF tif) { return (tif.tif_flags&TIF_FLAGS.TIFF_UPSAMPLED)!=0; }

		static int TIFFReadFile(TIFF tif, byte[] buf, int size) { return tif.tif_readproc(tif.tif_clientdata, buf, size); }
		static int TIFFWriteFile(TIFF tif, byte[] buf, int size) { return tif.tif_writeproc(tif.tif_clientdata, buf, size); }
		static uint TIFFSeekFile(TIFF tif, uint off, SEEK whence) { return tif.tif_seekproc(tif.tif_clientdata, off, whence); }
		static int TIFFCloseFile(TIFF tif) { return tif.tif_closeproc(tif.tif_clientdata); }
		static uint TIFFGetFileSize(TIFF tif) { return tif.tif_sizeproc(tif.tif_clientdata); }

		// Default Read/Seek/Write definitions.
		static bool ReadOK(TIFF tif, byte[] buf, int size) { return tif.tif_readproc(tif.tif_clientdata, buf, size)==size; }

		static bool ReadOK(TIFF tif, TIFFHeader header)
		{
			byte[] buf=new byte[8];
			if(!ReadOK(tif, buf, buf.Length)) return false;
			header.tiff_magic=BitConverter.ToUInt16(buf, 0);
			header.tiff_version=BitConverter.ToUInt16(buf, 2);
			header.tiff_diroff=BitConverter.ToUInt32(buf, 4);
			return true;
		}

		static bool ReadOK(TIFF tif, out ushort val)
		{
			byte[] buf=new byte[2];
			val=0;
			if(!ReadOK(tif, buf, buf.Length)) return false;
			val=BitConverter.ToUInt16(buf, 0);
			return true;
		}

		static bool ReadOK(TIFF tif, out uint val)
		{
			byte[] buf=new byte[4];
			val=0;
			if(!ReadOK(tif, buf, buf.Length)) return false;
			val=BitConverter.ToUInt32(buf, 0);
			return true;
		}

		static bool ReadOK(TIFF tif, List<TIFFDirEntry> val, ushort count)
		{
			byte[] buf=new byte[count*12];
			val.Clear();
			if(!ReadOK(tif, buf, buf.Length)) return false;

			int index=0;
			for(int i=0; i<count; i++)
			{
				TIFFDirEntry entry=new TIFFDirEntry();
				entry.tdir_tag=BitConverter.ToUInt16(buf, index); index+=2;
				entry.tdir_type=BitConverter.ToUInt16(buf, index); index+=2;
				entry.tdir_count=BitConverter.ToUInt32(buf, index); index+=4;
				entry.tdir_offset=BitConverter.ToUInt32(buf, index); index+=4;
				val.Add(entry);
			}
			return true;
		}

		static bool SeekOK(TIFF tif, uint off) { return tif.tif_seekproc(tif.tif_clientdata, off, SEEK.SET)==off; }
		static bool WriteOK(TIFF tif, byte[] buf, int size) { return tif.tif_writeproc(tif.tif_clientdata, buf, size)==size; }

		static bool WriteOK(TIFF tif, TIFFHeader header)
		{
			byte[] buf=new byte[8];
			BitConverter.GetBytes(header.tiff_magic).CopyTo(buf, 0);
			BitConverter.GetBytes(header.tiff_version).CopyTo(buf, 2);
			BitConverter.GetBytes(header.tiff_diroff).CopyTo(buf, 4);
			return tif.tif_writeproc(tif.tif_clientdata, buf, buf.Length)==buf.Length;
		}

		static bool WriteOK(TIFF tif, ushort val)
		{
			return tif.tif_writeproc(tif.tif_clientdata, BitConverter.GetBytes(val), 2)==2;
		}

		static bool WriteOK(TIFF tif, uint val)
		{
			return tif.tif_writeproc(tif.tif_clientdata, BitConverter.GetBytes(val), 4)==4;
		}

		static bool WriteOK(TIFF tif, List<TIFFDirEntry> val, ushort count)
		{
			byte[] buf=new byte[val.Count*12];

			int index=0;
			for(int i=0; i<val.Count; i++)
			{
				TIFFDirEntry entry=val[i];
				BitConverter.GetBytes(entry.tdir_tag).CopyTo(buf, index); index+=2;
				BitConverter.GetBytes(entry.tdir_type).CopyTo(buf, index); index+=2;
				BitConverter.GetBytes(entry.tdir_count).CopyTo(buf, index); index+=4;
				BitConverter.GetBytes(entry.tdir_offset).CopyTo(buf, index); index+=4;
			}
			return tif.tif_writeproc(tif.tif_clientdata, buf, buf.Length)==count;
		}

		// NB: the uint32 casts are to silence certain ANSI-C compilers
		// TIFFhowmany(x, y) (((uint32)x < (0xffffffff - (uint32)(y-1))) ? \
		//		((((uint32)(x))+(((uint32)(y))-1))/((uint32)(y))) : 0U)
		static int TIFFhowmany(int x, int y) { return (int)TIFFhowmany((uint)x, (uint)y); }
		static uint TIFFhowmany(uint x, uint y) { return (x<(0xffffffffu-(uint)(y-1)))?((x+y-1)/y):0; }

		// TIFFhowmany8(x) (((x)&0x07)?((uint32)(x)>>3)+1:(uint32)(x)>>3)
		static uint TIFFhowmany8(int x) { return ((x&0x07)!=0)?(((uint)x)>>3)+1:((uint)x)>>3; }
		static uint TIFFhowmany8(uint x) { return ((x&0x07)!=0)?(x>>3)+1:x>>3; }

		// TIFFroundup(x, y) (TIFFhowmany(x,y)*(y))
		static int TIFFroundup(int x, int y) { return ((x+y-1)/y)*y; }
		static uint TIFFroundup(uint x, uint y) { return ((x+y-1)/y)*y; }

		// Safe multiply which returns zero if there is an integer overflow
		static uint TIFFSafeMultiply(uint v, uint m)
		{
			long ret=(long)v*m;
			return (ret<0xffffffff)?(uint)ret:0;
		}

		static int TIFFSafeMultiply(int v, int m)
		{
			long ret=(long)v*m;
			return (ret<0x7fffffff)?(int)ret:0;
		}
	}
}
