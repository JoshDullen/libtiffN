// tif_open.cs
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
	public static partial class libtiff
	{
		static readonly uint[] tif_typemask=new uint[]
		{
			0,			// TIFF_NOTYPE
			0x000000ff,	// TIFF_BYTE
			0xffffffff,	// TIFF_ASCII
			0x0000ffff,	// TIFF_SHORT
			0xffffffff,	// TIFF_LONG
			0xffffffff,	// TIFF_RATIONAL
			0x000000ff,	// TIFF_SBYTE
			0x000000ff,	// TIFF_UNDEFINED
			0x0000ffff,	// TIFF_SSHORT
			0xffffffff,	// TIFF_SLONG
			0xffffffff,	// TIFF_SRATIONAL
			0xffffffff,	// TIFF_FLOAT
			0xffffffff,	// TIFF_DOUBLE
			0xffffffff	// TIFF_IFD
		};

		static readonly int[] tif_typeshift=new int[]
		{
			0,		// TIFF_NOTYPE
			24,		// TIFF_BYTE
			0,		// TIFF_ASCII
			16,		// TIFF_SHORT
			0,		// TIFF_LONG
			0,		// TIFF_RATIONAL
			24,		// TIFF_SBYTE
			24,		// TIFF_UNDEFINED
			16,		// TIFF_SSHORT
			0,		// TIFF_SLONG
			0,		// TIFF_SRATIONAL
			0,		// TIFF_FLOAT
			0,		// TIFF_DOUBLE
			0		// TIFF_IFD
		};

		//static readonly int[] litTypeshift=new int[]
		//{
		//0,		// TIFF_NOTYPE
		//0,		// TIFF_BYTE
		//0,		// TIFF_ASCII
		//0,		// TIFF_SHORT
		//0,		// TIFF_LONG
		//0,		// TIFF_RATIONAL
		//0,		// TIFF_SBYTE
		//0,		// TIFF_UNDEFINED
		//0,		// TIFF_SSHORT
		//0,		// TIFF_SLONG
		//0,		// TIFF_SRATIONAL
		//0,		// TIFF_FLOAT
		//0,		// TIFF_DOUBLE
		//0			// TIFF_IFD
		//};

		// Initialize the shift & mask tables, and the
		// byte swapping state according to the file
		// contents and the machine architecture.
		static void TIFFInitOrder(TIFF tif, int magic)
		{
			if(magic==TIFF_BIGENDIAN) tif.tif_flags|=TIF_FLAGS.TIFF_SWAB;
			else tif.tif_flags&=~TIF_FLAGS.TIFF_SWAB;
		}

		static O TIFFgetMode(string mode, string module)
		{
			O m=O.ERROR;

			switch(mode[0])
			{
				case 'r':
					m=O.RDONLY;
					if(mode.Length>1&&mode[1]=='+') m=O.RDWR;
					break;
				case 'w':
				case 'a':
					m=O.RDWR|O.CREAT;
					if(mode[0]=='w') m|=O.TRUNC;
					break;
				default:
					TIFFErrorExt(null, module, "\"{0}\": Bad mode", mode);
					break;
			}
			return m;
		}

		public static TIFF TIFFClientOpen(string name, string mode, Stream clientdata, TIFFReadWriteProc readproc, TIFFReadWriteProc writeproc, TIFFSeekProc seekproc, TIFFCloseProc closeproc, TIFFSizeProc sizeproc)
		{
			string module="TIFFClientOpen";

			O m=TIFFgetMode(mode, module);
			if(m==O.ERROR) return null;

			TIFF tif=null;
			try
			{
				tif=new TIFF();
			}
			catch
			{
				TIFFErrorExt(clientdata, module, "{0}: Out of memory (TIFF structure)", name);
				return null;
			}

			tif.tif_name=name;
			tif.tif_mode=m&~(O.CREAT|O.TRUNC);
			tif.tif_curdir=0xffff;			// non-existent directory
			tif.tif_curoff=0;
			tif.tif_curstrip=0xffffffff;	// invalid strip
			tif.tif_row=0xffffffff;			// read/write pre-increment
			tif.tif_clientdata=clientdata;

			if(readproc==null||writeproc==null||seekproc==null||closeproc==null||sizeproc==null)
			{
				TIFFErrorExt(clientdata, module, "One of the client procedures is NULL pointer.");
				return null;
			}

			tif.tif_readproc=readproc;
			tif.tif_writeproc=writeproc;
			tif.tif_seekproc=seekproc;
			tif.tif_closeproc=closeproc;
			tif.tif_sizeproc=sizeproc;
			TIFFSetDefaultCompressionState(tif);	// setup default state

			// Default is to return data MSB2LSB and enable the
			// use of memory-mapped files and strip chopping when
			// a file is opened read-only.
			tif.tif_flags=TIF_FLAGS.FILLORDER_MSB2LSB;

#if STRIPCHOP_DEFAULT
			if(m==O.RDONLY||m==O.RDWR) tif.tif_flags|=TIF_FLAGS.TIFF_STRIPCHOP;
#endif

			// Process library-specific flags in the open mode string.
			// The following flags may be used to control intrinsic library
			// behaviour that may or may not be desirable (usually for
			// compatibility with some application that claims to support
			// TIFF but only supports some braindead idea of what the
			// vendor thinks TIFF is):
			//
			// 'l'	use little-endian byte order for creating a file
			// 'b'	use big-endian byte order for creating a file
			// 'L'	read/write information using LSB2MSB bit order
			// 'B'	read/write information using MSB2LSB bit order
			// 'H'	read/write information using host bit order
			// 'C'	enable strip chopping support when reading
			// 'c'	disable strip chopping support
			// 'h'	read TIFF header only, do not load the first IFD
			//
			// The use of the 'l' and 'b' flags is strongly discouraged.
			// These flags are provided solely because numerous vendors,
			// typically on the PC, do not correctly support TIFF; they
			// only support the Intel little-endian byte order. This
			// support is not configured by default because it supports
			// the violation of the TIFF spec that says that readers *MUST*
			// support both byte orders. It is strongly recommended that
			// you not use this feature except to deal with busted apps
			// that write invalid TIFF. And even in those cases you should
			// bang on the vendors to fix their software.
			//
			// The 'L', 'B', and 'H' flags are intended for applications
			// that can optimize operations on data by using a particular
			// bit order. By default the library returns data in MSB2LSB
			// bit order for compatibiltiy with older versions of this
			// library. Returning data in the bit order of the native cpu
			// makes the most sense but also requires applications to check
			// the value of the FillOrder tag; something they probably do
			// not do right now.
			//
			// The 'C' and 'c' flags are provided because the library support
			// for chopping up large strips into multiple smaller strips is not
			// application-transparent and as such can cause problems. The 'c'
			// option permits applications that only want to look at the tags,
			// for example, to get the unadulterated TIFF tag information.
			foreach(char cp in mode)
			{
				switch(cp)
				{
					case 'b': if((m&O.CREAT)==O.CREAT) tif.tif_flags|=TIF_FLAGS.TIFF_SWAB;
						break;
					case 'l':
						break;
					case 'B':
						tif.tif_flags=(tif.tif_flags&~TIF_FLAGS.TIFF_FILLORDER)|TIF_FLAGS.FILLORDER_MSB2LSB;
						break;
					case 'L':
						tif.tif_flags=(tif.tif_flags&~TIF_FLAGS.TIFF_FILLORDER)|TIF_FLAGS.FILLORDER_LSB2MSB;
						break;
					case 'H':
						tif.tif_flags=(tif.tif_flags&~TIF_FLAGS.TIFF_FILLORDER)|HOST_FILLORDER;
						break;
					case 'C': if(m==O.RDONLY) tif.tif_flags|=TIF_FLAGS.TIFF_STRIPCHOP;
						break;
					case 'c': if(m==O.RDONLY) tif.tif_flags&=~TIF_FLAGS.TIFF_STRIPCHOP;
						break;
					case 'h':
						tif.tif_flags|=TIF_FLAGS.TIFF_HEADERONLY;
						break;
				}
			}

			// Read in TIFF header.
			if((tif.tif_mode&O.TRUNC)==O.TRUNC||!ReadOK(tif, tif.tif_header))
			{
				if(tif.tif_mode==O.RDONLY)
				{
					TIFFErrorExt(tif.tif_clientdata, name, "Cannot read TIFF header");
					goto bad;
				}

				// Setup header and write.
				tif.tif_header.tiff_magic=(tif.tif_flags&TIF_FLAGS.TIFF_SWAB)==TIF_FLAGS.TIFF_SWAB?TIFF_BIGENDIAN:TIFF_LITTLEENDIAN;
				tif.tif_header.tiff_version=TIFF_VERSION;
				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)==TIF_FLAGS.TIFF_SWAB) TIFFSwab(ref tif.tif_header.tiff_version);
				tif.tif_header.tiff_diroff=0;	// filled in later

				// The doc for "fopen" for some STD_C_LIBs says that if you
				// open a file for modify ("+"), then you must fseek (or
				// fflush?) between any freads and fwrites. This is not
				// necessary on most systems, but has been shown to be needed
				// on Solaris.
				TIFFSeekFile(tif, 0, SEEK.SET);

				if(!WriteOK(tif, tif.tif_header))
				{
					TIFFErrorExt(tif.tif_clientdata, name, "Error writing TIFF header");
					goto bad;
				}

				// Setup the byte order handling.
				TIFFInitOrder(tif, tif.tif_header.tiff_magic);

				// Setup default directory.
				if(!TIFFDefaultDirectory(tif)) goto bad;

				tif.tif_diroff=0;
				tif.tif_dirlist.Clear();
				tif.tif_dirnumber=0;
				return tif;
			}

			// Setup the byte order handling.
#if MDI_SUPPORT
			if(tif.tif_header.tiff_magic!=TIFF_BIGENDIAN&&tif.tif_header.tiff_magic!=TIFF_LITTLEENDIAN&&tif.tif_header.tiff_magic!=MDI_LITTLEENDIAN)
			{
				TIFFErrorExt(tif.tif_clientdata, name, "Not a TIFF or MDI file, bad magic number {0} (0x{1:X4})", tif.tif_header.tiff_magic, tif.tif_header.tiff_magic);
				goto bad;
			}
#else
			if(tif.tif_header.tiff_magic!=TIFF_BIGENDIAN&&tif.tif_header.tiff_magic!=TIFF_LITTLEENDIAN)
			{
				TIFFErrorExt(tif.tif_clientdata, name, "Not a TIFF file, bad magic number {0} (0x{1:X4})", tif.tif_header.tiff_magic, tif.tif_header.tiff_magic);
				goto bad;
			}
#endif

			TIFFInitOrder(tif, tif.tif_header.tiff_magic);

			// Swap header if required.
			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)==TIF_FLAGS.TIFF_SWAB)
			{
				TIFFSwab(ref tif.tif_header.tiff_version);
				TIFFSwab(ref tif.tif_header.tiff_diroff);
			}

			// Now check version (if needed, it's been byte-swapped).
			// Note that this isn't actually a version number, it's a
			// magic number that doesn't change (stupid).
			if(tif.tif_header.tiff_version==TIFF_BIGTIFF_VERSION)
			{
				TIFFErrorExt(tif.tif_clientdata, name, "This is a BigTIFF file. This format not supported\nby this version of libtiff.");
				goto bad;
			}

			if(tif.tif_header.tiff_version!=TIFF_VERSION)
			{
				TIFFErrorExt(tif.tif_clientdata, name, "Not a TIFF file, bad version number {0} (0x{1:X4})", tif.tif_header.tiff_version, tif.tif_header.tiff_version);
				goto bad;
			}

			tif.tif_flags|=TIF_FLAGS.TIFF_MYBUFFER;
			tif.tif_rawcp=0;
			tif.tif_rawdata=null;
			tif.tif_rawdatasize=0;

			// Sometimes we do not want to read the first directory (for example,
			// it may be broken) and want to proceed to other directories. I this
			// case we use the TIFF_HEADERONLY flag to open file and return
			// immediately after reading TIFF header.
			if((tif.tif_flags&TIF_FLAGS.TIFF_HEADERONLY)==TIF_FLAGS.TIFF_HEADERONLY) return tif;

			// Setup initial directory.
			switch(mode[0])
			{
				case 'r':
					tif.tif_nextdiroff=tif.tif_header.tiff_diroff;
					if(TIFFReadDirectory(tif))
					{
						tif.tif_rawcc=0;
						tif.tif_flags|=TIF_FLAGS.TIFF_BUFFERSETUP;
						return tif;
					}
					break;
				case 'a':
					// New directories are automatically append
					// to the end of the directory chain when they
					// are written out (see TIFFWriteDirectory).
					if(!TIFFDefaultDirectory(tif)) goto bad;
					return tif;
			}

bad:
			tif.tif_mode=O.RDONLY; // XXX avoid flush
			TIFFCleanup(tif);

			return null;
		}

		// Query functions to access private data.

		// Return open file's name.
		public static string TIFFFileName(TIFF tif)
		{
			return tif.tif_name;
		}

		// Set the file name.
		public static string TIFFSetFileName(TIFF tif, string name)
		{
			string old_name=tif.tif_name;
			tif.tif_name=name;
			return old_name;
		}

		// Return open file's I/O descriptor.
		public static Stream TIFFFileno(TIFF tif)
		{
			return tif.tif_fd;
		}

		// Set open file's I/O descriptor, and return previous value.
		public static Stream TIFFSetFileno(TIFF tif, Stream fd)
		{
			Stream old_fd=tif.tif_fd;
			tif.tif_fd=fd;
			return old_fd;
		}

		// Return open file's clientdata.
		public static Stream TIFFClientdata(TIFF tif)
		{
			return tif.tif_clientdata;
		}

		// Set open file's clientdata, and return previous value.
		public static Stream TIFFSetClientdata(TIFF tif, Stream newvalue)
		{
			Stream m=tif.tif_clientdata;
			tif.tif_clientdata=newvalue;
			return m;
		}

		// Return read/write mode.
		public static O TIFFGetMode(TIFF tif)
		{
			return tif.tif_mode;
		}

		// Return read/write mode.
		public static O TIFFSetMode(TIFF tif, O mode)
		{
			O old_mode=tif.tif_mode;
			tif.tif_mode=mode;
			return old_mode;
		}

		// Return nonzero if file is organized in
		// tiles; zero if organized as strips.
		public static bool TIFFIsTiled(TIFF tif)
		{
			return isTiled(tif);
		}

		// Return current row being read/written.
		public static uint TIFFCurrentRow(TIFF tif)
		{
			return tif.tif_row;
		}

		// Return index of the current directory.
		public static ushort TIFFCurrentDirectory(TIFF tif)
		{
			return tif.tif_curdir;
		}

		// Return current strip.
		public static uint TIFFCurrentStrip(TIFF tif)
		{
			return tif.tif_curstrip;
		}

		// Return current tile.
		public static uint TIFFCurrentTile(TIFF tif)
		{
			return tif.tif_curtile;
		}

		// Return nonzero if the file has byte-swapped data.
		public static bool TIFFIsByteSwapped(TIFF tif)
		{
			return ((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)==TIF_FLAGS.TIFF_SWAB);
		}

		// Return nonzero if the data is returned up-sampled.
		public static bool TIFFIsUpSampled(TIFF tif)
		{
			return isUpSampled(tif);
		}

		// Return nonzero if the data is returned in MSB-to-LSB bit order.
		public static bool TIFFIsMSB2LSB(TIFF tif)
		{
			return isFillOrder(tif, TIF_FLAGS.FILLORDER_MSB2LSB);
		}

		// Return nonzero if given file was written in big-endian order.
		public static bool TIFFIsBigEndian(TIFF tif)
		{
			return (tif.tif_header.tiff_magic==TIFF_BIGENDIAN);
		}

		// Return pointer to file read method.
		public static TIFFReadWriteProc TIFFGetReadProc(TIFF tif)
		{
			return tif.tif_readproc;
		}

		// Return pointer to file write method.
		public static TIFFReadWriteProc TIFFGetWriteProc(TIFF tif)
		{
			return tif.tif_writeproc;
		}

		// Return pointer to file seek method.
		public static TIFFSeekProc TIFFGetSeekProc(TIFF tif)
		{
			return tif.tif_seekproc;
		}

		// Return pointer to file close method.
		public static TIFFCloseProc TIFFGetCloseProc(TIFF tif)
		{
			return tif.tif_closeproc;
		}

		// Return pointer to file size requesting method.
		public static TIFFSizeProc TIFFGetSizeProc(TIFF tif)
		{
			return tif.tif_sizeproc;
		}
	}

	// Stuff
	[Flags]
	public enum O
	{
		ERROR=-1,		// ERROR
		RDONLY=0x0000,	// open for reading only
		WRONLY=0x0001,	// open for writing only
		RDWR=0x0002,	// open for reading and writing
		APPEND=0x0008,	// writes done at eof
		CREAT=0x0100,	// create and open file
		TRUNC=0x0200,	// open and truncate
		EXCL=0x0400		// open only if file doesn't already exist
	}
}
