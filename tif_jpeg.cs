#if JPEG_SUPPORT
// tif_jpeg.cs
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
// JPEG Compression support per TIFF Technical Note #2
// (*not* per the original TIFF 6.0 spec).
//
// This file is simply an interface to the libjpeg library written by
// the Independent JPEG Group. You need release 5 or later of the IJG
// code, which you can find on the Internet at ftp.uu.net:/graphics/jpeg/.
//
// Contributed by Tom Lane <tgl@sss.pgh.pa.us>.

using System;
using System.Collections.Generic;
using System.IO;

using Free.Ports.LibJpeg;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// State block for each open TIFF file using
		// libjpeg to do JPEG compression/decompression.
		//
		// libjpeg's visible state is either a jpeg_compress_struct
		// or jpeg_decompress_struct depending on which way we
		// are going. comm can be used to refer to the fields
		// which are common to both.
		class JPEGState : ICodecState
		{
			internal jpeg_common comm;		// jpeg_compress or jpeg_decompress
			internal bool cinfo_initialized;

			internal jpeg_error_mgr err=new jpeg_error_mgr();					// libjpeg error manager

			// The following two members could be a union, but
			// they're small enough that it's not worth the effort.
			internal jpeg_destination_mgr dest=new jpeg_destination_mgr();	// data dest for compression
			internal jpeg_source_mgr src=new jpeg_source_mgr();				// data source for decompression

			// private state
			internal TIFF tif;				// back link needed by some code
			internal PHOTOMETRIC photometric;	// copy of PhotometricInterpretation
			internal ushort h_sampling;		// luminance sampling factors
			internal ushort v_sampling;
			internal int bytesperline;		// decompressed bytes per scanline

			// pointers to intermediate buffers when processing downsampled data
			internal byte[][][] ds_buffer=new byte[libjpeg.MAX_COMPONENTS][][];
			internal int scancount;			// number of "scanlines" accumulated
			internal int samplesperclump;

			internal TIFFVGetMethod vgetparent;		// super-class method
			internal TIFFVSetMethod vsetparent;		// super-class method
			internal TIFFPrintMethod printdir;		// super-class method
			internal TIFFStripMethod defsparent;	// super-class method
			internal TIFFTileMethod deftparent;		// super-class method

			// pseudo-tag fields
			internal byte[] jpegtables;				// JPEGTables tag value, or null
			internal uint jpegtables_length;		// number of bytes in same
			internal int jpegquality;				// Compression quality level
			internal JPEGCOLORMODE jpegcolormode;	// Auto RGB<=>YCbCr convert?
			internal JPEGTABLESMODE jpegtablesmode;	// What to put in JPEGTables

			internal bool ycbcrsampling_fetched;
			internal uint recvparams;	// encoded Class 2 session params
			internal string subaddress;	// subaddress string
			internal uint recvtime;		// time spent receiving (secs)
			internal string faxdcs;		// encoded fax parameters (DCS, Table 2/T.30)

			internal byte[][] scanlineBuffer=new byte[1][];
		}

		static readonly List<TIFFFieldInfo> jpegFieldInfo=MakeJpegFieldInfo();

		static List<TIFFFieldInfo> MakeJpegFieldInfo()
		{
			List<TIFFFieldInfo> ret=new List<TIFFFieldInfo>();
			ret.Add(new TIFFFieldInfo(TIFFTAG.JPEGTABLES, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.JPEG_JPEGTABLES, false, true, "JPEGTables"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.JPEGQUALITY, 0, 0, TIFFDataType.TIFF_ANY, FIELD.PSEUDO, true, false, ""));

			ret.Add(new TIFFFieldInfo(TIFFTAG.JPEGCOLORMODE, 0, 0, TIFFDataType.TIFF_ANY, FIELD.PSEUDO, false, false, ""));
			ret.Add(new TIFFFieldInfo(TIFFTAG.JPEGTABLESMODE, 0, 0, TIFFDataType.TIFF_ANY, FIELD.PSEUDO, false, false, ""));

			// Specific for JPEG in faxes
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXRECVPARAMS, 1, 1, TIFFDataType.TIFF_LONG, FIELD.JPEG_RECVPARAMS, true, false, "FaxRecvParams"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXSUBADDRESS, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.JPEG_SUBADDRESS, true, false, "FaxSubAddress"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXRECVTIME, 1, 1, TIFFDataType.TIFF_LONG, FIELD.JPEG_RECVTIME, true, false, "FaxRecvTime"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXDCS, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.JPEG_FAXDCS, true, false, "FaxDcs"));

			return ret;
		}

		// libjpeg interface layer.
		//
		// We use exceptions to return control to libtiff
		// when a fatal error is encountered within the JPEG
		// library. We also direct libjpeg error and warning
		// messages through the appropriate libtiff handlers.

		// Error handling routines (these replace corresponding
		// IJG routines from jerror.cs). These are used for both
		// compression and decompression.
		static void TIFFjpeg_error_exit(jpeg_common cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;
			string buffer=cinfo.err.format_message(cinfo);
			TIFFErrorExt(sp.tif.tif_clientdata, "JPEGLib", buffer);		// display the error message
			libjpeg.jpeg_abort(cinfo);									// clean up libjpeg state
			throw new Exception("jpeg_error_exit");						// return to libtiff caller
		}

		// This routine is invoked only for warning messages,
		// since error_exit does its own thing and trace_level
		// is never set > 0.
		static void TIFFjpeg_output_message(jpeg_common cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;
			string buffer=cinfo.err.format_message(cinfo);
			TIFFWarningExt(sp.tif.tif_clientdata, "JPEGLib", buffer);
		}

		static bool TIFFjpeg_create_compress(JPEGState sp)
		{
			// initialize JPEG error handling
			jpeg_compress c=new jpeg_compress();
			sp.comm=c;
			sp.comm.err=libjpeg.jpeg_std_error(sp.err);
			sp.err.error_exit=TIFFjpeg_error_exit;
			sp.err.output_message=TIFFjpeg_output_message;

			c.client_data=sp;

			try
			{
				libjpeg.jpeg_create_compress(c);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_create_decompress(JPEGState sp)
		{
			// initialize JPEG error handling
			jpeg_decompress d=new jpeg_decompress();
			sp.comm=d;
			sp.comm.err=libjpeg.jpeg_std_error(sp.err);
			sp.err.error_exit=TIFFjpeg_error_exit;
			sp.err.output_message=TIFFjpeg_output_message;

			d.client_data=sp;

			try
			{
				libjpeg.jpeg_create_decompress(d);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_set_defaults(JPEGState sp)
		{
			try
			{
				libjpeg.jpeg_set_defaults((jpeg_compress)sp.comm);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_set_colorspace(JPEGState sp, J_COLOR_SPACE colorspace)
		{
			try
			{
				libjpeg.jpeg_set_colorspace((jpeg_compress)sp.comm, colorspace);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_set_quality(JPEGState sp, int quality, bool force_baseline)
		{
			try
			{
				libjpeg.jpeg_set_quality((jpeg_compress)sp.comm, quality, force_baseline);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_suppress_tables(JPEGState sp, bool suppress)
		{
			try
			{
				libjpeg.jpeg_suppress_tables((jpeg_compress)sp.comm, suppress);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_start_compress(JPEGState sp, bool write_all_tables)
		{
			try
			{
				libjpeg.jpeg_start_compress((jpeg_compress)sp.comm, write_all_tables);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static int TIFFjpeg_write_scanlines(JPEGState sp, byte[][] scanlines, int num_lines)
		{
			try
			{
				return (int)libjpeg.jpeg_write_scanlines((jpeg_compress)sp.comm, scanlines, (uint)num_lines);
			}
			catch
			{
				return -1;
			}
		}

		static int TIFFjpeg_write_raw_data(JPEGState sp, byte[][][] data, int num_lines)
		{
			try
			{
				return (int)libjpeg.jpeg_write_raw_data((jpeg_compress)sp.comm, data, (uint)num_lines);
			}
			catch
			{
				return -1;
			}
		}

		static bool TIFFjpeg_finish_compress(JPEGState sp)
		{
			try
			{
				libjpeg.jpeg_finish_compress((jpeg_compress)sp.comm);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_write_tables(JPEGState sp)
		{
			try
			{
				libjpeg.jpeg_write_tables((jpeg_compress)sp.comm);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static CONSUME_INPUT TIFFjpeg_read_header(JPEGState sp, bool require_image)
		{
			try
			{
				return libjpeg.jpeg_read_header((jpeg_decompress)sp.comm, require_image);
			}
			catch
			{
				return (CONSUME_INPUT)(-1);
			}
		}

		static bool TIFFjpeg_start_decompress(JPEGState sp)
		{
			try
			{
				libjpeg.jpeg_start_decompress((jpeg_decompress)sp.comm);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static int TIFFjpeg_read_scanlines(JPEGState sp, byte[][] scanlines, int max_lines)
		{
			try
			{
				return (int)libjpeg.jpeg_read_scanlines((jpeg_decompress)sp.comm, scanlines, (uint)max_lines);
			}
			catch
			{
				return -1;
			}
		}

		static int TIFFjpeg_read_raw_data(JPEGState sp, byte[][][] data, int max_lines)
		{
			try
			{
				return (int)libjpeg.jpeg_read_raw_data((jpeg_decompress)sp.comm, data, (uint)max_lines);
			}
			catch
			{
				return -1;
			}
		}

		static bool TIFFjpeg_finish_decompress(JPEGState sp)
		{
			try
			{
				return libjpeg.jpeg_finish_decompress((jpeg_decompress)sp.comm);
			}
			catch
			{
				return false;
			}
		}

		static bool TIFFjpeg_abort(JPEGState sp)
		{
			try
			{
				libjpeg.jpeg_abort((jpeg_decompress)sp.comm);
			}
			catch
			{
				return false;
			}
			return true;
		}

		static bool TIFFjpeg_destroy(JPEGState sp)
		{
			try
			{
                var jd = sp.comm as jpeg_decompress;
                if (jd != null) {
                    libjpeg.jpeg_destroy(jd);
                }
			}
			catch
			{
				return false;
			}
			return true;
		}

		static byte[][] TIFFjpeg_alloc_sarray(JPEGState sp, uint samplesperrow, uint numrows)
		{
			try
			{
				return libjpeg.alloc_sarray(sp.comm, samplesperrow, numrows);
			}
			catch
			{
				return null;
			}
		}

		// JPEG library destination data manager.
		// These routines direct compressed data from libjpeg into the
		// libtiff output buffer.
		static void std_init_destination(jpeg_compress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;
			TIFF tif=sp.tif;

			sp.dest.output_bytes=tif.tif_rawdata;
			sp.dest.next_output_byte=0;
			sp.dest.free_in_buffer=tif.tif_rawdatasize;
		}

		static bool std_empty_output_buffer(jpeg_compress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;
			TIFF tif=sp.tif;

			// the entire buffer has been filled
			tif.tif_rawcc=tif.tif_rawdatasize;
			TIFFFlushData1(tif);
			sp.dest.output_bytes=tif.tif_rawdata;
			sp.dest.next_output_byte=0;
			sp.dest.free_in_buffer=tif.tif_rawdatasize;

			return true;
		}

		static void std_term_destination(jpeg_compress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;
			TIFF tif=sp.tif;

			tif.tif_rawcp=(uint)sp.dest.next_output_byte;
			tif.tif_rawcc=tif.tif_rawdatasize-sp.dest.free_in_buffer;
			// NB: libtiff does the final buffer flush
		}

		static void TIFFjpeg_data_dest(JPEGState sp, TIFF tif)
		{
			((jpeg_compress)sp.comm).dest=sp.dest;
			sp.dest.init_destination=std_init_destination;
			sp.dest.empty_output_buffer=std_empty_output_buffer;
			sp.dest.term_destination=std_term_destination;
		}

		// Alternate destination manager for outputting to JPEGTables field.
		static void tables_init_destination(jpeg_compress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;

			// while building, jpegtables_length is allocated buffer size
			sp.dest.output_bytes=sp.jpegtables;
			sp.dest.next_output_byte=0;
			sp.dest.free_in_buffer=sp.jpegtables_length;
		}

		static bool tables_empty_output_buffer(jpeg_compress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;
			byte[] newbuf=null;

			// the entire buffer has been filled; enlarge it by 1000 bytes
			try
			{
				newbuf=new byte[sp.jpegtables_length+1000];
				sp.jpegtables.CopyTo(newbuf, 0);
			}
			catch
			{
				libjpeg.ERREXIT1(cinfo, J_MESSAGE_CODE.JERR_OUT_OF_MEMORY, 100);
			}

			sp.dest.output_bytes=newbuf;
			sp.dest.next_output_byte=(int)sp.jpegtables_length;
			sp.dest.free_in_buffer=1000;
			sp.jpegtables=newbuf;
			sp.jpegtables_length+=1000;
			return true;
		}

		static void tables_term_destination(jpeg_compress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;

			// set tables length to number of bytes actually emitted
			sp.jpegtables_length-=sp.dest.free_in_buffer;
		}

		static bool TIFFjpeg_tables_dest(JPEGState sp, TIFF tif)
		{
			// Allocate a working buffer for building tables.
			// Initial size is 1000 bytes, which is usually adequate.
			sp.jpegtables_length=1000;
			try
			{
				sp.jpegtables=new byte[sp.jpegtables_length];
			}
			catch
			{
				sp.jpegtables_length=0;
				TIFFErrorExt(sp.tif.tif_clientdata, "TIFFjpeg_tables_dest", "No space for JPEGTables");
				return false;
			}

			((jpeg_compress)sp.comm).dest=sp.dest;
			sp.dest.init_destination=tables_init_destination;
			sp.dest.empty_output_buffer=tables_empty_output_buffer;
			sp.dest.term_destination=tables_term_destination;

			return true;
		}

		// JPEG library source data manager.
		// These routines supply compressed data to libjpeg.
		static void std_init_source(jpeg_decompress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;
			TIFF tif=sp.tif;

			sp.src.input_bytes=tif.tif_rawdata;
			sp.src.next_input_byte=0;
			sp.src.bytes_in_buffer=tif.tif_rawcc;
		}

		static readonly byte[] dummy_EOI=new byte[2] { 0xFF, libjpeg.JPEG_EOI };

		static bool std_fill_input_buffer(jpeg_decompress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;

			// Should never get here since entire strip/tile is
			// read into memory before the decompressor is called,
			// and thus was supplied by init_source.
			libjpeg.WARNMS(cinfo, J_MESSAGE_CODE.JWRN_JPEG_EOF);

			// insert a fake EOI marker
			sp.src.input_bytes=dummy_EOI;
			sp.src.next_input_byte=0;
			sp.src.bytes_in_buffer=2;
			return true;
		}

		static void std_skip_input_data(jpeg_decompress cinfo, int num_bytes)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;

			if(num_bytes>0)
			{
				if(num_bytes>(int)sp.src.bytes_in_buffer)
				{ // oops, buffer overrun
					std_fill_input_buffer(cinfo);
				}
				else
				{
					sp.src.next_input_byte+=num_bytes;
					sp.src.bytes_in_buffer-=(uint)num_bytes;
				}
			}
		}

		static void std_term_source(jpeg_decompress cinfo, bool readExtraBytes, out byte[] data)
		{
			// No work necessary here
			// Or must we update tif.tif_rawcp, tif.tif_rawcc ???
			// (if so, need empty tables_term_source!)

			// Also ignore readExtraBytes
			data=null;
		}

		static void TIFFjpeg_data_src(JPEGState sp, TIFF tif)
		{
			((jpeg_decompress)sp.comm).src=sp.src;
			sp.src.init_source=std_init_source;
			sp.src.fill_input_buffer=std_fill_input_buffer;
			sp.src.skip_input_data=std_skip_input_data;
			sp.src.resync_to_restart=libjpeg.jpeg_resync_to_restart;
			sp.src.term_source=std_term_source;
			sp.src.bytes_in_buffer=0;		// for safety
			sp.src.input_bytes=null;
			sp.src.next_input_byte=0;
		}

		// Alternate source manager for reading from JPEGTables.
		// We can share all the code except for the init routine.
		static void tables_init_source(jpeg_decompress cinfo)
		{
			JPEGState sp=(JPEGState)cinfo.client_data;

			sp.src.input_bytes=sp.jpegtables;
			sp.src.next_input_byte=0;
			sp.src.bytes_in_buffer=sp.jpegtables_length;
		}

		static void TIFFjpeg_tables_src(JPEGState sp, TIFF tif)
		{
			TIFFjpeg_data_src(sp, tif);
			sp.src.init_source=tables_init_source;
		}

		// Allocate downsampled-data buffers needed for downsampled I/O.
		// We use values computed in jpeg_start_compress or jpeg_start_decompress.
		// We use libjpeg's allocator so that buffers will be released automatically
		// when done with strip/tile.
		// This is also a handy place to compute samplesperclump, bytesperline.
		static bool alloc_downsampled_buffers(TIFF tif, jpeg_component_info[] comp_info, int num_components)
		{
			JPEGState sp=tif.tif_data as JPEGState;

			byte[][] buf;
			int samples_per_clump=0;

			for(int ci=0; ci<num_components; ci++)
			{
				jpeg_component_info compptr=comp_info[ci];

				samples_per_clump+=compptr.h_samp_factor*compptr.v_samp_factor;
				try
				{
					buf=TIFFjpeg_alloc_sarray(sp, compptr.width_in_blocks*libjpeg.DCTSIZE, (uint)compptr.v_samp_factor*libjpeg.DCTSIZE);
				}
				catch
				{
					return false;
				}

				sp.ds_buffer[ci]=buf;
			}

			sp.samplesperclump=samples_per_clump;
			return true;
		}

		// JPEG Decoding.

		static bool JPEGSetupDecode(TIFF tif)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;

			JPEGInitializeLibJPEG(tif, false, true);

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if(!sp.comm.is_decompressor) throw new Exception("!sp.comm.is_decompressor");
#endif

			// Read JPEGTables if it is present
			if(TIFFFieldSet(tif, FIELD.JPEG_JPEGTABLES))
			{
				TIFFjpeg_tables_src(sp, tif);
				if(TIFFjpeg_read_header(sp, false)!=CONSUME_INPUT.JPEG_HEADER_TABLES_ONLY)
				{
					TIFFErrorExt(tif.tif_clientdata, "JPEGSetupDecode", "Bogus JPEGTables field");
					return false;
				}
			}

			// Grab parameters that are same for all strips/tiles
			sp.photometric=td.td_photometric;
			switch(sp.photometric)
			{
				case PHOTOMETRIC.YCBCR:
					sp.h_sampling=td.td_ycbcrsubsampling[0];
					sp.v_sampling=td.td_ycbcrsubsampling[1];
					break;
				default:
					// TIFF 6.0 forbids subsampling of all other color spaces
					sp.h_sampling=1;
					sp.v_sampling=1;
					break;
			}

			// Set up for reading normal data
			TIFFjpeg_data_src(sp, tif);
			tif.tif_postdecode=TIFFNoPostDecode; // override byte swapping
			return true;
		}

		// Set up for decoding a strip or tile.
		static bool JPEGPreDecode(TIFF tif, ushort sampleNumber)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;
			string module="JPEGPreDecode";

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if(!sp.comm.is_decompressor) throw new Exception("!sp.comm.is_decompressor");
#endif

			jpeg_decompress d=(jpeg_decompress)sp.comm;

			// Reset decoder state from any previous strip/tile,
			// in case application didn't read the whole strip.
			if(!TIFFjpeg_abort(sp)) return false;

			// Read the header for this strip/tile.
			if(TIFFjpeg_read_header(sp, true)!=CONSUME_INPUT.JPEG_HEADER_OK) return false;

			// Check image parameters and set decompression parameters.
			uint segment_width=td.td_imagewidth;
			uint segment_height=td.td_imagelength-tif.tif_row;
			if(isTiled(tif))
			{
				segment_width=td.td_tilewidth;
				segment_height=td.td_tilelength;
				sp.bytesperline=TIFFTileRowSize(tif);
			}
			else
			{
				if(segment_height>td.td_rowsperstrip) segment_height=td.td_rowsperstrip;
				sp.bytesperline=TIFFOldScanlineSize(tif);
			}

			if(td.td_planarconfig==PLANARCONFIG.SEPARATE&&sampleNumber>0)
			{
				// For PC 2, scale down the expected strip/tile size
				// to match a downsampled component
				segment_width=TIFFhowmany(segment_width, sp.h_sampling);
				segment_height=TIFFhowmany(segment_height, sp.v_sampling);
			}

			if(d.image_width<segment_width||d.image_height<segment_height)
				TIFFWarningExt(tif.tif_clientdata, module, "Improper JPEG strip/tile size, expected {0}x{1}, got {2}x{3}", segment_width, segment_height, d.image_width, d.image_height);

			if(d.image_width>segment_width||d.image_height>segment_height)
			{
				// This case could be dangerous, if the strip or tile size has
				// been reported as less than the amount of data jpeg will
				// return, some potential security issues arise. Catch this
				// case and error out.
				TIFFErrorExt(tif.tif_clientdata, module, "JPEG strip/tile size exceeds expected dimensions, expected {0}x{1}, got {2}x{3}", segment_width, segment_height, d.image_width, d.image_height);
				return false;
			}

			if(d.num_components!=(td.td_planarconfig==PLANARCONFIG.CONTIG?(int)td.td_samplesperpixel:1))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Improper JPEG component count");
				return false;
			}

			if(d.data_precision!=td.td_bitspersample)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Improper JPEG data precision");
				return false;
			}

			if(td.td_planarconfig==PLANARCONFIG.CONTIG)
			{
				// Component 0 should have expected sampling factors
				if(d.comp_info[0].h_samp_factor!=sp.h_sampling||d.comp_info[0].v_samp_factor!=sp.v_sampling)
				{
					TIFFWarningExt(tif.tif_clientdata, module, "Improper JPEG sampling factors {0},{1}\nApparently should be {2},{3}.", d.comp_info[0].h_samp_factor, d.comp_info[0].v_samp_factor, sp.h_sampling, sp.v_sampling);

					// There are potential security issues here
					// for decoders that have already allocated
					// buffers based on the expected sampling
					// factors. Lets check the sampling factors
					// dont exceed what we were expecting.
					if(d.comp_info[0].h_samp_factor>sp.h_sampling||d.comp_info[0].v_samp_factor>sp.v_sampling)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Cannot honour JPEG sampling factors that exceed those specified.");
						return false;
					}

					// XXX: Files written by the Intergraph software
					// has different sampling factors stored in the
					// TIFF tags and in the JPEG structures. We will
					// try to deduce Intergraph files by the presense
					// of the tag 33918.
					if(TIFFFindFieldInfo(tif, TIFFTAG.INTERGRAPH_PACKET_DATA, TIFFDataType.TIFF_ANY)==null)
					{
						TIFFWarningExt(tif.tif_clientdata, module, "Decompressor will try reading with sampling {0},{1}.", d.comp_info[0].h_samp_factor, d.comp_info[0].v_samp_factor);

						sp.h_sampling=(ushort)d.comp_info[0].h_samp_factor;
						sp.v_sampling=(ushort)d.comp_info[0].v_samp_factor;
					}
				}

				// Rest should have sampling factors 1,1
				for(int ci=1; ci<d.num_components; ci++)
				{
					if(d.comp_info[ci].h_samp_factor!=1||d.comp_info[ci].v_samp_factor!=1)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Improper JPEG sampling factors");
						return false;
					}
				}
			}
			else
			{
				// PC 2's single component should have sampling factors 1,1
				if(d.comp_info[0].h_samp_factor!=1||d.comp_info[0].v_samp_factor!=1)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "Improper JPEG sampling factors");
					return false;
				}
			}

			bool downsampled_output=false;
			if(td.td_planarconfig==PLANARCONFIG.CONTIG&&sp.photometric==PHOTOMETRIC.YCBCR&&sp.jpegcolormode==JPEGCOLORMODE.RGB)
			{
				// Convert YCbCr to RGB
				d.jpeg_color_space=J_COLOR_SPACE.JCS_YCbCr;
				d.out_color_space=J_COLOR_SPACE.JCS_RGB;
			}
			else
			{
				// Suppress colorspace handling
				d.jpeg_color_space=J_COLOR_SPACE.JCS_UNKNOWN;
				d.out_color_space=J_COLOR_SPACE.JCS_UNKNOWN;
				if(td.td_planarconfig==PLANARCONFIG.CONTIG&&(sp.h_sampling!=1||sp.v_sampling!=1)) downsampled_output=true;
				// XXX what about up-sampling?
			}

			if(downsampled_output)
			{
				// Need to use raw-data interface to libjpeg
				d.raw_data_out=true;
				tif.tif_decoderow=JPEGDecodeRaw;
				tif.tif_decodestrip=JPEGDecodeRaw;
				tif.tif_decodetile=JPEGDecodeRaw;
			}
			else
			{
				// Use normal interface to libjpeg
				d.raw_data_out=false;
				tif.tif_decoderow=JPEGDecode;
				tif.tif_decodestrip=JPEGDecode;
				tif.tif_decodetile=JPEGDecode;
			}

			// Start JPEG decompressor
			if(!TIFFjpeg_start_decompress(sp)) return false;

			// Allocate downsampled-data buffers if needed
			if(downsampled_output)
			{
				if(!alloc_downsampled_buffers(tif, d.comp_info, d.num_components)) return false;
				sp.scancount=libjpeg.DCTSIZE;	// mark buffer empty
			}
			return true;
		}

		// Decode a chunk of pixels.
		// "Standard" case: returned data is not downsampled.
		static bool JPEGDecode(TIFF tif, byte[] buf, int cc, ushort s)
		{
			JPEGState sp=tif.tif_data as JPEGState;

			jpeg_decompress d=(jpeg_decompress)sp.comm;

			int nrows=cc/sp.bytesperline;
			if((cc%sp.bytesperline)!=0) TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "fractional scanline not read");

			if(nrows>(int)d.image_height) nrows=(int)d.image_height;

			// data is expected to be read in multiples of a scanline
			if(nrows!=0)
			{
				if(sp.scanlineBuffer[0]==null||sp.scanlineBuffer[0].Length<sp.bytesperline)
					sp.scanlineBuffer[0]=new byte[sp.bytesperline];

				int buf_ind=0;
				do
				{
					// In the libjpeg6b 8bit case. We read directly into the TIFF buffer.
					if(TIFFjpeg_read_scanlines(sp, sp.scanlineBuffer, 1)!=1) return false;
					sp.scanlineBuffer[0].CopyTo(buf, buf_ind);

					tif.tif_row++;
					buf_ind+=sp.bytesperline;
					cc-=sp.bytesperline;
					nrows--;
				} while(nrows>0);
			}

			// Close down the decompressor if we've finished the strip or tile.
			return (d.output_scanline<d.output_height)||TIFFjpeg_finish_decompress(sp);
		}

		// Decode a chunk of pixels.
		// Returned data is downsampled per sampling factors.
		static bool JPEGDecodeRaw(TIFF tif, byte[] buf, int cc, ushort s)
		{
			JPEGState sp=tif.tif_data as JPEGState;

			jpeg_decompress d=(jpeg_decompress)sp.comm;

			int nrows=(int)d.image_height;

			// data is expected to be read in multiples of a scanline
			if(nrows!=0)
			{
				// Cb,Cr both have sampling factors 1, so this is correct
				uint clumps_per_line=d.comp_info[1].downsampled_width;
				int samples_per_clump=sp.samplesperclump;

				do
				{
					// Reload downsampled-data buffer if needed
					if(sp.scancount>=libjpeg.DCTSIZE)
					{
						int n=d.max_v_samp_factor*libjpeg.DCTSIZE;
						if(TIFFjpeg_read_raw_data(sp, sp.ds_buffer, n)!=n) return false;
						sp.scancount=0;
					}

					// Fastest way to unseparate data is to make one pass
					// over the scanline for each row of each component.
					int clumpoffset=0; // first sample in clump
					int buf_offset=0;
					for(int ci=0; ci<d.num_components; ci++)
					{
						jpeg_component_info compptr=d.comp_info[ci];
						int hsamp=compptr.h_samp_factor;
						int vsamp=compptr.v_samp_factor;

						for(int ypos=0; ypos<vsamp; ypos++)
						{
							byte[] ds=sp.ds_buffer[ci][sp.scancount*vsamp+ypos];
							uint inptr=0;
							int outptr=buf_offset+clumpoffset;
							uint nclump;

							if(hsamp==1)
							{ // fast path for at least Cb and Cr
								for(nclump=clumps_per_line; nclump-->0; )
								{
									buf[outptr]=ds[inptr++];
									outptr+=samples_per_clump;
								}
							}
							else
							{ // general case
								for(nclump=clumps_per_line; nclump-->0; )
								{
									for(int xpos=0; xpos<hsamp; xpos++) buf[outptr+xpos]=ds[inptr++];
									outptr+=samples_per_clump;
								}
							}
							clumpoffset+=hsamp;
						}
					}

					sp.scancount++;
					tif.tif_row+=sp.v_sampling;
					// increment/decrement of buf and cc is still incorrect, but should not matter
					// TODO: resolve this
					buf_offset+=sp.bytesperline;
					cc-=sp.bytesperline;
					nrows-=sp.v_sampling;
				} while(nrows>0);
			}

			// Close down the decompressor if done.
			return d.output_scanline<d.output_height||TIFFjpeg_finish_decompress(sp);
		}

		// JPEG Encoding.

		static void unsuppress_quant_table(JPEGState sp, int tblno)
		{
			jpeg_compress c=(jpeg_compress)sp.comm;

			JQUANT_TBL qtbl=c.quant_tbl_ptrs[tblno];
			if(qtbl!=null) qtbl.sent_table=false;
		}

		static void unsuppress_huff_table(JPEGState sp, int tblno)
		{
			jpeg_compress c=(jpeg_compress)sp.comm;

			JHUFF_TBL htbl=c.dc_huff_tbl_ptrs[tblno];
			if(htbl!=null) htbl.sent_table=false;
			htbl=c.ac_huff_tbl_ptrs[tblno];
			if(htbl!=null) htbl.sent_table=false;
		}

		static bool prepare_JPEGTables(TIFF tif)
		{
			JPEGState sp=tif.tif_data as JPEGState;

			JPEGInitializeLibJPEG(tif, false, false);

			// Initialize quant tables for current quality setting
			if(!TIFFjpeg_set_quality(sp, sp.jpegquality, false)) return false;

			// Mark only the tables we want for output
			// NB: chrominance tables are currently used only with YCbCr
			if(!TIFFjpeg_suppress_tables(sp, true)) return false;

			if((sp.jpegtablesmode&JPEGTABLESMODE.QUANT)!=0)
			{
				unsuppress_quant_table(sp, 0);
				if(sp.photometric==PHOTOMETRIC.YCBCR) unsuppress_quant_table(sp, 1);
			}
			if((sp.jpegtablesmode&JPEGTABLESMODE.HUFF)!=0)
			{
				unsuppress_huff_table(sp, 0);
				if(sp.photometric==PHOTOMETRIC.YCBCR) unsuppress_huff_table(sp, 1);
			}

			// Direct libjpeg output into jpegtables
			if(!TIFFjpeg_tables_dest(sp, tif)) return false;

			// Emit tables-only datastream
			if(!TIFFjpeg_write_tables(sp)) return false;

			return true;
		}

		static bool JPEGSetupEncode(TIFF tif)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;
			string module="JPEGSetupEncode";

			JPEGInitializeLibJPEG(tif, true, false);

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if(sp.comm.is_decompressor) throw new Exception("sp.comm.is_decompressor");
#endif

			jpeg_compress c=(jpeg_compress)sp.comm;

			// Initialize all JPEG parameters to default values.
			// Note that jpeg_set_defaults needs legal values for
			// in_color_space and input_components.
			c.in_color_space=J_COLOR_SPACE.JCS_UNKNOWN;
			c.input_components=1;
			if(!TIFFjpeg_set_defaults(sp)) return false;

			// Set per-file parameters
			sp.photometric=td.td_photometric;
			switch(sp.photometric)
			{
				case PHOTOMETRIC.YCBCR:
					sp.h_sampling=td.td_ycbcrsubsampling[0];
					sp.v_sampling=td.td_ycbcrsubsampling[1];

					// A ReferenceBlackWhite field *must* be present since the
					// default value is inappropriate for YCbCr. Fill in the
					// proper value if application didn't set it.
					object[] ap=new object[2];
					if(!TIFFGetField(tif, TIFFTAG.REFERENCEBLACKWHITE, ap))
					{
						double[] refbw=new double[6];
						long top=1<<td.td_bitspersample;
						refbw[0]=0;
						refbw[1]=top-1;
						refbw[2]=top>>1;
						refbw[3]=refbw[1];
						refbw[4]=refbw[2];
						refbw[5]=refbw[1];
						TIFFSetField(tif, TIFFTAG.REFERENCEBLACKWHITE, refbw);
					}
					break;
				case PHOTOMETRIC.PALETTE: // disallowed by Tech Note
				case PHOTOMETRIC.MASK:
					TIFFErrorExt(tif.tif_clientdata, module, "PhotometricInterpretation {0} not allowed for JPEG", sp.photometric);
					return false;
				default: // TIFF 6.0 forbids subsampling of all other color spaces
					sp.h_sampling=1;
					sp.v_sampling=1;
					break;
			}

			// Verify miscellaneous parameters

			// This would need work if libtiff ever supports different
			// depths for different components, or if libjpeg ever supports
			// run-time selection of depth. Neither is imminent.
			if(td.td_bitspersample!=libjpeg.BITS_IN_JSAMPLE)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "BitsPerSample {0} not allowed for JPEG", td.td_bitspersample);
				return false;
			}
			c.data_precision=td.td_bitspersample;

			if(isTiled(tif))
			{
				if((td.td_tilelength%(sp.v_sampling*libjpeg.DCTSIZE))!=0)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "JPEG tile height must be multiple of {0}", sp.v_sampling*libjpeg.DCTSIZE);
					return false;
				}
				if((td.td_tilewidth%(sp.h_sampling*libjpeg.DCTSIZE))!=0)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "JPEG tile width must be multiple of {0}", sp.h_sampling*libjpeg.DCTSIZE);
					return false;
				}
			}
			else
			{
				if(td.td_rowsperstrip<td.td_imagelength&&(td.td_rowsperstrip%(sp.v_sampling*libjpeg.DCTSIZE))!=0)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "RowsPerStrip must be multiple of {0} for JPEG", sp.v_sampling*libjpeg.DCTSIZE);
					return false;
				}
			}

			// Create a JPEGTables field if appropriate
			if((sp.jpegtablesmode&(JPEGTABLESMODE.QUANT|JPEGTABLESMODE.HUFF))!=0)
			{
				if(sp.jpegtables==null||
					(sp.jpegtables[0]==0&&sp.jpegtables[1]==0&&sp.jpegtables[2]==0&&sp.jpegtables[3]==0&&
					sp.jpegtables[4]==0&&sp.jpegtables[5]==0&&sp.jpegtables[6]==0&&sp.jpegtables[7]==0))
				{
					if(!prepare_JPEGTables(tif)) return false;

					// Mark the field present
					// Can't use TIFFSetField since BEENWRITING is already set!
					tif.tif_flags|=TIF_FLAGS.TIFF_DIRTYDIRECT;
					TIFFSetFieldBit(tif, FIELD.JPEG_JPEGTABLES);
				}
			}
			else
			{
				// We do not support application-supplied JPEGTables,
				// so mark the field not present.
				TIFFClrFieldBit(tif, FIELD.JPEG_JPEGTABLES);
			}

			// Direct libjpeg output to libtiff's output buffer
			TIFFjpeg_data_dest(sp, tif);

			return true;
		}

		// Set encoding state at the start of a strip or tile.
		static bool JPEGPreEncode(TIFF tif, ushort sampleNumber)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;
			string module="JPEGPreEncode";

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if(sp.comm.is_decompressor) throw new Exception("sp.comm.is_decompressor");
#endif

			jpeg_compress c=(jpeg_compress)sp.comm;

			// Set encoding parameters for this strip/tile.
			uint segment_width, segment_height;
			if(isTiled(tif))
			{
				segment_width=td.td_tilewidth;
				segment_height=td.td_tilelength;
				sp.bytesperline=TIFFTileRowSize(tif);
			}
			else
			{
				segment_width=td.td_imagewidth;
				segment_height=td.td_imagelength-tif.tif_row;
				if(segment_height>td.td_rowsperstrip) segment_height=td.td_rowsperstrip;
				sp.bytesperline=TIFFOldScanlineSize(tif);
			}

			if(td.td_planarconfig==PLANARCONFIG.SEPARATE&&sampleNumber>0)
			{
				// for PC 2, scale down the strip/tile size
				// to match a downsampled component
				segment_width=TIFFhowmany(segment_width, sp.h_sampling);
				segment_height=TIFFhowmany(segment_height, sp.v_sampling);
			}

			if(segment_width>65535||segment_height>65535)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Strip/tile too large for JPEG");
				return false;
			}

			c.image_width=segment_width;
			c.image_height=segment_height;

			bool downsampled_input=false;

			if(td.td_planarconfig==PLANARCONFIG.CONTIG)
			{
				c.input_components=td.td_samplesperpixel;
				if(sp.photometric==PHOTOMETRIC.YCBCR)
				{
					if(sp.jpegcolormode==JPEGCOLORMODE.RGB)
					{
						c.in_color_space=J_COLOR_SPACE.JCS_RGB;
					}
					else
					{
						c.in_color_space=J_COLOR_SPACE.JCS_YCbCr;
						if(sp.h_sampling!=1||sp.v_sampling!=1) downsampled_input=true;
					}
					if(!TIFFjpeg_set_colorspace(sp, J_COLOR_SPACE.JCS_YCbCr)) return false;

					// Set Y sampling factors;
					// we assume jpeg_set_colorspace() set the rest to 1
					c.comp_info[0].h_samp_factor=sp.h_sampling;
					c.comp_info[0].v_samp_factor=sp.v_sampling;
				}
				else
				{
					c.in_color_space=J_COLOR_SPACE.JCS_UNKNOWN;
					if(!TIFFjpeg_set_colorspace(sp, J_COLOR_SPACE.JCS_UNKNOWN)) return false;
					// jpeg_set_colorspace set all sampling factors to 1
				}
			}
			else
			{
				c.input_components=1;
				c.in_color_space=J_COLOR_SPACE.JCS_UNKNOWN;
				if(!TIFFjpeg_set_colorspace(sp, J_COLOR_SPACE.JCS_UNKNOWN)) return false;
				c.comp_info[0].component_id=sampleNumber;
				// jpeg_set_colorspace() set sampling factors to 1
				if(sp.photometric==PHOTOMETRIC.YCBCR&&sampleNumber>0)
				{
					c.comp_info[0].quant_tbl_no=1;
					c.comp_info[0].dc_tbl_no=1;
					c.comp_info[0].ac_tbl_no=1;
				}
			}

			// ensure libjpeg won't write any extraneous markers
			c.write_JFIF_header=false;
			c.write_Adobe_marker=false;

			// set up table handling correctly
			if(!TIFFjpeg_set_quality(sp, sp.jpegquality, false)) return false;
			if((sp.jpegtablesmode&JPEGTABLESMODE.QUANT)==0)
			{
				unsuppress_quant_table(sp, 0);
				unsuppress_quant_table(sp, 1);
			}

			c.optimize_coding=(sp.jpegtablesmode&JPEGTABLESMODE.HUFF)==0;

			if(downsampled_input)
			{
				// Need to use raw-data interface to libjpeg
				c.raw_data_in=true;
				tif.tif_encoderow=JPEGEncodeRaw;
				tif.tif_encodestrip=JPEGEncodeRaw;
				tif.tif_encodetile=JPEGEncodeRaw;
			}
			else
			{
				// Use normal interface to libjpeg
				c.raw_data_in=false;
				tif.tif_encoderow=JPEGEncode;
				tif.tif_encodestrip=JPEGEncode;
				tif.tif_encodetile=JPEGEncode;
			}

			// Start JPEG compressor
			if(!TIFFjpeg_start_compress(sp, false)) return false;

			// Allocate downsampled-data buffers if needed
			if(downsampled_input)
			{
				if(!alloc_downsampled_buffers(tif, c.comp_info, c.num_components)) return false;
			}
			sp.scancount=0;

			return true;
		}

		// Encode a chunk of pixels.
		// "Standard" case: incoming data is not downsampled.
		static bool JPEGEncode(TIFF tif, byte[] buf, int cc, ushort s)
		{
			JPEGState sp=tif.tif_data as JPEGState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			// data is expected to be supplied in multiples of a scanline
			int nrows=cc/sp.bytesperline;
			if((cc%sp.bytesperline)!=0) TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "fractional scanline discarded");

			// The last strip will be limited to image size
			if(!isTiled(tif)&&tif.tif_row+nrows>tif.tif_dir.td_imagelength)
				nrows=(int)(tif.tif_dir.td_imagelength-tif.tif_row);

			if(nrows>0)
			{
				if(sp.scanlineBuffer[0]==null||sp.scanlineBuffer[0].Length<sp.bytesperline)
					sp.scanlineBuffer[0]=new byte[sp.bytesperline];

				int buf_ind=0;
				while((nrows--)>0)
				{
					Array.Copy(buf, buf_ind, sp.scanlineBuffer[0], 0, sp.bytesperline);
					if(TIFFjpeg_write_scanlines(sp, sp.scanlineBuffer, 1)!=1) return false;
					if(nrows>0) tif.tif_row++;
					buf_ind+=sp.bytesperline;
				}
			}

			return true;
		}

		// Encode a chunk of pixels.
		// Incoming data is expected to be downsampled per sampling factors.
		static bool JPEGEncodeRaw(TIFF tif, byte[] buf, int cc, ushort s)
		{
			JPEGState sp=tif.tif_data as JPEGState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			jpeg_compress c=(jpeg_compress)sp.comm;

			int samples_per_clump=sp.samplesperclump;

			// data is expected to be supplied in multiples of a clumpline
			// a clumpline is equivalent to v_sampling desubsampled scanlines
			// TODO: the following calculation of bytesperclumpline, should substitute calculation of sp.bytesperline, except that it is per v_sampling lines
			int bytesperclumpline=(int)((((c.image_width+sp.h_sampling-1)/sp.h_sampling)*(sp.h_sampling*sp.v_sampling+2)*c.data_precision+7)/8);

			int nrows=(cc/bytesperclumpline)*sp.v_sampling;
			if((cc%bytesperclumpline)!=0)
				TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "fractional scanline discarded");

			// Cb,Cr both have sampling factors 1, so this is correct
			uint clumps_per_line=c.comp_info[1].downsampled_width;

			int buf_ind=0;
			while(nrows>0)
			{
				// Fastest way to separate the data is to make one pass
				// over the scanline for each row of each component.
				int clumpoffset=0;		// first sample in clump

				for(int ci=0; ci<c.num_components; ci++)
				{
					jpeg_component_info compptr=c.comp_info[ci];

					int hsamp=compptr.h_samp_factor;
					int vsamp=compptr.v_samp_factor;
					int padding=(int)(compptr.width_in_blocks*libjpeg.DCTSIZE-clumps_per_line*hsamp);

					for(int ypos=0; ypos<vsamp; ypos++)
					{
						int inptr=buf_ind+clumpoffset;
						byte[] ds=sp.ds_buffer[ci][sp.scancount*vsamp+ypos];
						int outptr=0;
						if(hsamp==1)
						{ // fast path for at least Cb and Cr
							for(uint nclump=clumps_per_line; nclump-->0; )
							{
								ds[outptr++]=buf[inptr];
								inptr+=samples_per_clump;
							}
						}
						else
						{ // general case
							for(uint nclump=clumps_per_line; nclump-->0; )
							{
								for(int xpos=0; xpos<hsamp; xpos++) ds[outptr++]=buf[inptr+xpos];
								inptr+=samples_per_clump;
							}
						}

						// pad each scanline as needed
						for(int xpos=0; xpos<padding; xpos++)
						{
							ds[outptr]=ds[outptr-1];
							outptr++;
						}
						clumpoffset+=hsamp;
					}
				}

				sp.scancount++;
				if(sp.scancount>=libjpeg.DCTSIZE)
				{
					int n=c.max_v_samp_factor*libjpeg.DCTSIZE;
					if(TIFFjpeg_write_raw_data(sp, sp.ds_buffer, n)!=n) return false;
					sp.scancount=0;
				}

				tif.tif_row+=sp.v_sampling;
				buf_ind+=sp.bytesperline;
				nrows-=sp.v_sampling;
			}

			return true;
		}

		// Finish up at the end of a strip or tile.
		static bool JPEGPostEncode(TIFF tif)
		{
			JPEGState sp=tif.tif_data as JPEGState;

			jpeg_compress c=(jpeg_compress)sp.comm;

			if(sp.scancount>0)
			{
				// Need to emit a partial bufferload of downsampled data.
				// Pad the data vertically.
				for(int ci=0; ci<c.num_components; ci++)
				{
					jpeg_component_info compptr=c.comp_info[ci];
					int vsamp=compptr.v_samp_factor;
					uint row_width=compptr.width_in_blocks*libjpeg.DCTSIZE;
					for(int ypos=sp.scancount*vsamp; ypos<libjpeg.DCTSIZE*vsamp; ypos++)
						Array.Copy(sp.ds_buffer[ci][ypos-1], sp.ds_buffer[ci][ypos], row_width);
				}

				int n=c.max_v_samp_factor*libjpeg.DCTSIZE;
				if(TIFFjpeg_write_raw_data(sp, sp.ds_buffer, n)!=n) return false;
			}

			return TIFFjpeg_finish_compress((JPEGState)tif.tif_data);
		}

		static void JPEGCleanup(TIFF tif)
		{
			JPEGState sp=tif.tif_data as JPEGState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			tif.tif_tagmethods.vgetfield=sp.vgetparent;
			tif.tif_tagmethods.vsetfield=sp.vsetparent;
			tif.tif_tagmethods.printdir=sp.printdir;

			if(sp.cinfo_initialized) TIFFjpeg_destroy(sp); // release libjpeg resources
			sp.jpegtables=null; // tag value
			tif.tif_data=null; // release local state

			TIFFSetDefaultCompressionState(tif);
		}

		static void JPEGResetUpsampled(TIFF tif)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;

			// Mark whether returned data is up-sampled or not so TIFFStripSize
			// and TIFFTileSize return values that reflect the true amount of
			// data.
			tif.tif_flags&=~TIF_FLAGS.TIFF_UPSAMPLED;
			if(td.td_planarconfig==PLANARCONFIG.CONTIG)
			{
				if(td.td_photometric==PHOTOMETRIC.YCBCR&&
					sp.jpegcolormode==JPEGCOLORMODE.RGB)
				{
					tif.tif_flags|=TIF_FLAGS.TIFF_UPSAMPLED;
				}
				else
				{
#if notdef
					if(td.td_ycbcrsubsampling[0]!=1||td.td_ycbcrsubsampling[1]!=1) ; // XXX what about up-sampling?
#endif
				}
			}

			// Must recalculate cached tile size in case sampling state changed.
			// Should we really be doing this now if image size isn't set? 
			if(tif.tif_tilesize>0) tif.tif_tilesize=isTiled(tif)?TIFFTileSize(tif):-1;

			if(tif.tif_scanlinesize>0&&tif.tif_scanlinesize!=unchecked((uint)-1)) tif.tif_scanlinesize=(uint)TIFFScanlineSize(tif);
		}

		static bool JPEGVSetField(TIFF tif, TIFFTAG tag, TIFFDataType dt, object[] ap)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			switch(tag)
			{
				case TIFFTAG.JPEGTABLES:
					uint v32=__GetAsUint(ap, 0);
					if(v32==0) return false;// XXX
					TIFFsetByteArray(ref sp.jpegtables, ap[1] as byte[], v32);
					sp.jpegtables_length=v32;
					TIFFSetFieldBit(tif, FIELD.JPEG_JPEGTABLES);
					break;
				case TIFFTAG.JPEGQUALITY:
					sp.jpegquality=__GetAsInt(ap, 0);
					return true;			// pseudo tag
				case TIFFTAG.JPEGCOLORMODE:
					sp.jpegcolormode=(JPEGCOLORMODE)__GetAsInt(ap, 0);
					JPEGResetUpsampled(tif);
					return true;			// pseudo tag
				case TIFFTAG.JPEGTABLESMODE:
					sp.jpegtablesmode=(JPEGTABLESMODE)__GetAsInt(ap, 0);
					return true;			// pseudo tag
				case TIFFTAG.PHOTOMETRIC:
					bool ret_value=sp.vsetparent(tif, tag, dt, ap);
					JPEGResetUpsampled(tif);
					return ret_value;
				case TIFFTAG.YCBCRSUBSAMPLING:
					// mark the fact that we have a real ycbcrsubsampling!
					sp.ycbcrsampling_fetched=true;
					// should we be recomputing upsampling info here?
					return sp.vsetparent(tif, tag, dt, ap);
				case TIFFTAG.FAXRECVPARAMS:
					sp.recvparams=__GetAsUint(ap, 0);
					break;
				case TIFFTAG.FAXSUBADDRESS:
					sp.subaddress=ap[0] as string;
					break;
				case TIFFTAG.FAXRECVTIME:
					sp.recvtime=__GetAsUint(ap, 0);
					break;
				case TIFFTAG.FAXDCS:
					sp.faxdcs=ap[0] as string;
					break;
				default:
					return sp.vsetparent(tif, tag, dt, ap);
			}

			TIFFFieldInfo fip=TIFFFieldWithTag(tif, tag);
			if(fip!=null) TIFFSetFieldBit(tif, fip.field_bit);
			else return false;

			tif.tif_flags|=TIF_FLAGS.TIFF_DIRTYDIRECT;
			return true;
		}

		// Some JPEG-in-TIFF produces do not emit the YCBCRSUBSAMPLING values in
		// the TIFF tags, but still use non-default (2,2) values within the jpeg
		// data stream itself. In order for TIFF applications to work properly
		// - for instance to get the strip buffer size right - it is imperative
		// that the subsampling be available before we start reading the image
		// data normally. This function will attempt to load the first strip in
		// order to get the sampling values from the jpeg data stream. Various
		// hacks in various places are done to ensure this function gets called
		// before the td_ycbcrsubsampling values are used from the directory structure,
		// including calling TIFFGetField() for the YCBCRSUBSAMPLING field from
		// TIFFStripSize(), and the printing code in tif_print.cs.
		//
		// Note that JPEGPreDeocode() will produce a fairly loud warning when the
		// discovered sampling does not match the default sampling (2,2) or whatever
		// was actually in the tiff tags.
		//
		// Problems:
		//	o	This code will cause one whole strip/tile of compressed data to be
		//		loaded just to get the tags right, even if the imagery is never read.
		//		It would be more efficient to just load a bit of the header, and
		//		initialize things from that.
		//
		// See the bug in bugzilla for details:
		//
		// http://bugzilla.remotesensing.org/show_bug.cgi?id=168
		//
		// Frank Warmerdam, July 2002

		static void JPEGFixupTestSubsampling(TIFF tif)
		{
#if CHECK_JPEG_YCBCR_SUBSAMPLING
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;

			JPEGInitializeLibJPEG(tif, false, false);

			// Some JPEG-in-TIFF files don't provide the ycbcrsampling tags,
			// and use a sampling schema other than the default 2,2. To handle
			// this we actually have to scan the header of a strip or tile of
			// jpeg data to get the sampling.
			if(!sp.comm.is_decompressor||sp.ycbcrsampling_fetched||td.td_photometric!=PHOTOMETRIC.YCBCR) return;

			sp.ycbcrsampling_fetched=true;
			if(TIFFIsTiled(tif))
			{
				if(!TIFFFillTile(tif, 0)) return;
			}
			else
			{
				if(!TIFFFillStrip(tif, 0)) return;
			}

			TIFFSetField(tif, TIFFTAG.YCBCRSUBSAMPLING, sp.h_sampling, sp.v_sampling);

			// We want to clear the loaded strip so the application has time
			// to set JPEGCOLORMODE or other behavior modifiers. This essentially
			// undoes the JPEGPreDecode triggers by TIFFFileStrip(). (#1936)
			int dummy=-1;
			tif.tif_curstrip=(uint)dummy;

#endif // CHECK_JPEG_YCBCR_SUBSAMPLING
		}

		static bool JPEGVGetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			JPEGState sp=tif.tif_data as JPEGState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			switch(tag)
			{
				case TIFFTAG.JPEGTABLES:
					ap[0]=sp.jpegtables_length;
					ap[1]=sp.jpegtables;
					break;
				case TIFFTAG.JPEGQUALITY:
					ap[0]=sp.jpegquality;
					break;
				case TIFFTAG.JPEGCOLORMODE:
					ap[0]=sp.jpegcolormode;
					break;
				case TIFFTAG.JPEGTABLESMODE:
					ap[0]=sp.jpegtablesmode;
					break;
				case TIFFTAG.YCBCRSUBSAMPLING:
					JPEGFixupTestSubsampling(tif);
					return sp.vgetparent(tif, tag, ap);
				case TIFFTAG.FAXRECVPARAMS:
					ap[0]=sp.recvparams;
					break;
				case TIFFTAG.FAXSUBADDRESS:
					ap[0]=sp.subaddress;
					break;
				case TIFFTAG.FAXRECVTIME:
					ap[0]=sp.recvtime;
					break;
				case TIFFTAG.FAXDCS:
					ap[0]=sp.faxdcs;
					break;
				default:
					return sp.vgetparent(tif, tag, ap);
			}
			return true;
		}

		static void JPEGPrintDir(TIFF tif, TextWriter fd, TIFFPRINT flags)
		{
			JPEGState sp=tif.tif_data as JPEGState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			if(TIFFFieldSet(tif, FIELD.JPEG_JPEGTABLES)) fd.WriteLine(" JPEG Tables: ({0} bytes)", sp.jpegtables_length);
			if(TIFFFieldSet(tif, FIELD.JPEG_RECVPARAMS)) fd.WriteLine(" Fax Receive Parameters: {0:X8}", sp.recvparams);
			if(TIFFFieldSet(tif, FIELD.JPEG_SUBADDRESS)) fd.WriteLine(" Fax SubAddress: {0}", sp.subaddress);
			if(TIFFFieldSet(tif, FIELD.JPEG_RECVTIME)) fd.WriteLine(" Fax Receive Time: {0} secs", sp.recvtime);
			if(TIFFFieldSet(tif, FIELD.JPEG_FAXDCS)) fd.WriteLine(" Fax DCS: {0}", sp.faxdcs);
		}

		static uint JPEGDefaultStripSize(TIFF tif, uint s)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;

			s=sp.defsparent(tif, s);
			if(s<td.td_imagelength) s=TIFFroundup(s, (uint)td.td_ycbcrsubsampling[1]*libjpeg.DCTSIZE);
			return s;
		}

		static void JPEGDefaultTileSize(TIFF tif, ref uint tw, ref uint th)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			TIFFDirectory td=tif.tif_dir;

			sp.deftparent(tif, ref tw, ref th);
			tw=TIFFroundup(tw, (uint)td.td_ycbcrsubsampling[0]*libjpeg.DCTSIZE);
			th=TIFFroundup(th, (uint)td.td_ycbcrsubsampling[1]*libjpeg.DCTSIZE);
		}

		// The JPEG library initialized used to be done in TIFFInitJPEG(), but
		// now that we allow a TIFF file to be opened in update mode it is necessary
		// to have some way of deciding whether compression or decompression is
		// desired other than looking at tif.tif_mode. We accomplish this by
		// examining {TILE/STRIP}BYTECOUNTS to see if there is a non-zero entry.
		// If so, we assume decompression is desired.
		//
		// This is tricky, because TIFFInitJPEG() is called while the directory is
		// being read, and generally speaking the BYTECOUNTS tag won't have been read
		// at that point. So we try to defer jpeg library initialization till we
		// do have that tag ... basically any access that might require the compressor
		// or decompressor that occurs after the reading of the directory.
		//
		// In an ideal world compressors or decompressors would be setup
		// at the point where a single tile or strip was accessed (for read or write)
		// so that stuff like update of missing tiles, or replacement of tiles could
		// be done. However, we aren't trying to crack that nut just yet ...
		//
		// NFW, Feb 3rd, 2003.
		static bool JPEGInitializeLibJPEG(TIFF tif, bool force_encode, bool force_decode)
		{
			JPEGState sp=tif.tif_data as JPEGState;
			uint[] byte_counts=null;
			bool data_is_empty=true;
			bool decompress;

			if(sp.cinfo_initialized)
			{
				if(force_encode&&sp.comm.is_decompressor) TIFFjpeg_destroy(sp);
				else if(force_decode&&!sp.comm.is_decompressor) TIFFjpeg_destroy(sp);
				else return true;

				sp.cinfo_initialized=false;
			}

			// Do we have tile data already? Make sure we initialize the
			// the state in decompressor mode if we have tile data, even if we
			// are not in read-only file access mode.
			object[] ap=new object[2];
			if(TIFFIsTiled(tif))
			{
				if(TIFFGetField(tif, TIFFTAG.TILEBYTECOUNTS, ap))
				{
					byte_counts=ap[0] as uint[];
					if(byte_counts!=null) data_is_empty=byte_counts[0]==0;
				}
			}
			else
			{
				if(TIFFGetField(tif, TIFFTAG.STRIPBYTECOUNTS, ap))
				{
					byte_counts=ap[0] as uint[];
					if(byte_counts!=null) data_is_empty=byte_counts[0]==0;
				}
			}

			if(force_decode) decompress=true;
			else if(force_encode) decompress=false;
			else if(tif.tif_mode==O.RDONLY) decompress=true;
			else if(data_is_empty) decompress=false;
			else decompress=true;

			// Initialize libjpeg.
			if(decompress)
			{
				if(!TIFFjpeg_create_decompress(sp)) return false;
			}
			else
			{
				if(!TIFFjpeg_create_compress(sp)) return false;
			}

			sp.cinfo_initialized=true;

			return true;
		}

		static bool TIFFInitJPEG(TIFF tif, COMPRESSION scheme)
		{
#if DEBUG
			if(scheme!=COMPRESSION.JPEG) throw new Exception("scheme!=COMPRESSION.JPEG");
#endif

			// Merge codec-specific tag information.
			if(!_TIFFMergeFieldInfo(tif, jpegFieldInfo))
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFInitJPEG", "Merging JPEG codec-specific tags failed");
				return false;
			}

			// Allocate state block so tag methods have storage to record values.
			JPEGState sp=null;
			try
			{
				tif.tif_data=sp=new JPEGState();
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFInitJPEG", "No space for JPEG state block");
				return false;
			}

			sp.tif=tif; // back link

			// Override parent get/set field methods.
			sp.vgetparent=tif.tif_tagmethods.vgetfield;
			tif.tif_tagmethods.vgetfield=JPEGVGetField;	// hook for codec tags
			sp.vsetparent=tif.tif_tagmethods.vsetfield;
			tif.tif_tagmethods.vsetfield=JPEGVSetField;	// hook for codec tags
			sp.printdir=tif.tif_tagmethods.printdir;
			tif.tif_tagmethods.printdir=JPEGPrintDir;	// hook for codec tags

			// Default values for codec-specific fields
			sp.jpegtables=null;
			sp.jpegtables_length=0;
			sp.jpegquality=75;			// Default IJG quality
			sp.jpegcolormode=JPEGCOLORMODE.RAW;
			sp.jpegtablesmode=JPEGTABLESMODE.QUANT|JPEGTABLESMODE.HUFF;

			sp.recvparams=0;
			sp.subaddress=null;
			sp.faxdcs=null;

			sp.ycbcrsampling_fetched=false;

			// Install codec methods.
			tif.tif_setupdecode=JPEGSetupDecode;
			tif.tif_predecode=JPEGPreDecode;
			tif.tif_decoderow=JPEGDecode;
			tif.tif_decodestrip=JPEGDecode;
			tif.tif_decodetile=JPEGDecode;
			tif.tif_setupencode=JPEGSetupEncode;
			tif.tif_preencode=JPEGPreEncode;
			tif.tif_postencode=JPEGPostEncode;
			tif.tif_encoderow=JPEGEncode;
			tif.tif_encodestrip=JPEGEncode;
			tif.tif_encodetile=JPEGEncode;
			tif.tif_cleanup=JPEGCleanup;
			sp.defsparent=tif.tif_defstripsize;
			tif.tif_defstripsize=JPEGDefaultStripSize;
			sp.deftparent=tif.tif_deftilesize;
			tif.tif_deftilesize=JPEGDefaultTileSize;
			tif.tif_flags|=TIF_FLAGS.TIFF_NOBITREV; // no bit reversal, please

			sp.cinfo_initialized=false;

			// Create a JPEGTables field if no directory has yet been created.
			// We do this just to ensure that sufficient space is reserved for
			// the JPEGTables field. It will be properly created the right
			// size later.
			if(tif.tif_diroff==0)
			{
				uint SIZE_OF_JPEGTABLES=2000;

				// The following line assumes incorrectly that all JPEG-in-TIFF files will have
				// a JPEGTABLES tag generated and causes null-filled JPEGTABLES tags to be written
				// when the JPEG data is placed with TIFFWriteRawStrip.  The field bit should be 
				// set, anyway, later when actual JPEGTABLES header is generated, so removing it 
				// here hopefully is harmless.
				//TIFFSetFieldBit(tif, FIELD.JPEG_JPEGTABLES);

				sp.jpegtables_length=SIZE_OF_JPEGTABLES;
				sp.jpegtables=new byte[sp.jpegtables_length];
			}

			// Mark the TIFFTAG.YCBCRSAMPLES as present even if it is not
			// see: JPEGFixupTestSubsampling().
			TIFFSetFieldBit(tif, FIELD.YCBCRSUBSAMPLING);

			return true;
		}
	}
}
#endif // JPEG_SUPPORT
