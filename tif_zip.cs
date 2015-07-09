#if ZIP_SUPPORT
// tif_zip.cs
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
// ZIP (aka Deflate) Compression Support
//
// This file is simply an interface to the zlib library written by
// Jean-loup Gailly and Mark Adler. You must use version 1.0 or later
// of the library: this code assumes the 1.0 API and also depends on
// the ability to write the zlib header multiple times (one per strip)
// which was not possible with versions prior to 0.95. Note also that
// older versions of this codec avoided this bug by supressing the header
// entirely. This means that files written with the old library cannot
// be read; they should be converted to a different compression scheme
// and then reconverted.
//
// The data format used by the zlib library is described in the files
// zlib-3.1.doc, deflate-1.1.doc and gzip-4.1.doc, available in the
// directory ftp://ftp.uu.net/pub/archiving/zip/doc. The library was
// last found at ftp://ftp.uu.net/pub/archiving/zip/zlib/zlib-0.99.tar.gz.

using System;
using System.Collections.Generic;
using System.Text;

using Free.Ports.zLib;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		[Flags]
		enum ZSTATE
		{
			None=0,
			INIT_DECODE=1,
			INIT_ENCODE=2,
		}

		// State block for each open TIFF
		// file using ZIP compression/decompression.
		class ZIPState : TIFFPredictorState
		{
			internal zlib.z_stream stream;
			internal int zipquality;		// compression level
			internal ZSTATE state;			// state flags

			internal new TIFFVGetMethod vgetparent;	// super-class method
			internal new TIFFVSetMethod vsetparent;	// super-class method
		}

		static bool ZIPSetupDecode(TIFF tif)
		{
			ZIPState sp=tif.tif_data as ZIPState;
			string module="ZIPSetupDecode";

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			// if we were last encoding, terminate this mode
			if((sp.state&ZSTATE.INIT_ENCODE)==ZSTATE.INIT_ENCODE)
			{
				zlib.deflateEnd(sp.stream);
				sp.state=ZSTATE.None;
			}

			if(zlib.inflateInit(sp.stream)!=zlib.Z_OK)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: {1}", tif.tif_name, sp.stream.msg);
				return false;
			}
			else
			{
				sp.state|=ZSTATE.INIT_DECODE;
				return true;
			}
		}

		// Setup state for decoding a strip.
		static bool ZIPPreDecode(TIFF tif, ushort sampleNumber)
		{
			ZIPState sp=tif.tif_data as ZIPState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			if((sp.state&ZSTATE.INIT_DECODE)!=ZSTATE.INIT_DECODE)
				tif.tif_setupdecode(tif);

			sp.stream.in_buf=tif.tif_rawdata;
			sp.stream.next_in=0;
			sp.stream.avail_in=tif.tif_rawcc;
			return zlib.inflateReset(sp.stream)==zlib.Z_OK;
		}

		static bool ZIPDecode(TIFF tif, byte[] op, int occ, ushort s)
		{
			ZIPState sp=tif.tif_data as ZIPState;
			string module="ZIPDecode";

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if((sp.state&ZSTATE.INIT_DECODE)!=ZSTATE.INIT_DECODE) throw new Exception("sp.state!=ZSTATE.INIT_DECODE");
#endif

			sp.stream.out_buf=op;
			sp.stream.next_out=0;
			sp.stream.avail_out=(uint)occ;
			do
			{
				int state=zlib.inflate(sp.stream, zlib.Z_PARTIAL_FLUSH);
				if(state==zlib.Z_STREAM_END) break;
				if(state==zlib.Z_DATA_ERROR)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: Decoding error at scanline {1}, {2}", tif.tif_name, tif.tif_row, sp.stream.msg);
					if(zlib.inflateSync(sp.stream)!=zlib.Z_OK) return false;
					continue;
				}
				if(state!=zlib.Z_OK)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: zlib error: {1}", tif.tif_name, sp.stream.msg);
					return false;
				}
			} while(sp.stream.avail_out>0);

			if(sp.stream.avail_out!=0)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Not enough data at scanline {1} (short {2} bytes)", tif.tif_name, tif.tif_row, sp.stream.avail_out);
				return false;
			}
			return true;
		}

		static bool ZIPSetupEncode(TIFF tif)
		{
			ZIPState sp=tif.tif_data as ZIPState;
			string module="ZIPSetupEncode";

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			if((sp.state&ZSTATE.INIT_DECODE)==ZSTATE.INIT_DECODE)
			{
				zlib.inflateEnd(sp.stream);
				sp.state=ZSTATE.None;
			}

			if(zlib.deflateInit(sp.stream, sp.zipquality)!=zlib.Z_OK)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: {1}", tif.tif_name, sp.stream.msg);
				return false;
			}
			else
			{
				sp.state=ZSTATE.INIT_ENCODE;
				return true;
			}
		}

		// Reset encoding state at the start of a strip.
		static bool ZIPPreEncode(TIFF tif, ushort sampleNumber)
		{
			ZIPState sp=tif.tif_data as ZIPState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			if((sp.state&ZSTATE.INIT_ENCODE)!=ZSTATE.INIT_ENCODE)
				tif.tif_setupencode(tif);

			sp.stream.out_buf=tif.tif_rawdata;
			sp.stream.next_out=0;
			sp.stream.avail_out=tif.tif_rawdatasize;
			return zlib.deflateReset(sp.stream)==zlib.Z_OK;
		}

		// Encode a chunk of pixels.
		static bool ZIPEncode(TIFF tif, byte[] bp, int cc, ushort s)
		{
			ZIPState sp=tif.tif_data as ZIPState;
			string module="ZIPEncode";

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if((sp.state&ZSTATE.INIT_ENCODE)!=ZSTATE.INIT_ENCODE) throw new Exception("sp.state!=ZSTATE.INIT_ENCODE");
#endif

			sp.stream.in_buf=bp;
			sp.stream.next_in=0;
			sp.stream.avail_in=(uint)cc;

			do
			{
				if(zlib.deflate(sp.stream, zlib.Z_NO_FLUSH)!=zlib.Z_OK)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: Encoder error: {1}", tif.tif_name, sp.stream.msg);
					return false;
				}

				if(sp.stream.avail_out==0)
				{
					tif.tif_rawcc=tif.tif_rawdatasize;
					TIFFFlushData1(tif);

					sp.stream.out_buf=tif.tif_rawdata;
					sp.stream.next_out=0;
					sp.stream.avail_out=tif.tif_rawdatasize;
				}
			} while(sp.stream.avail_in>0);

			return true;
		}

		// Finish off an encoded strip by flushing the last
		// string and tacking on an End Of Information code.
		static bool ZIPPostEncode(TIFF tif)
		{
			ZIPState sp=tif.tif_data as ZIPState;
			string module="ZIPPostEncode";
			int state;

			sp.stream.avail_in=0;
			do
			{
				state=zlib.deflate(sp.stream, zlib.Z_FINISH);
				switch(state)
				{
					case zlib.Z_STREAM_END:
					case zlib.Z_OK:
						if((int)sp.stream.avail_out!=(int)tif.tif_rawdatasize)
						{
							tif.tif_rawcc=tif.tif_rawdatasize-sp.stream.avail_out;
							TIFFFlushData1(tif);
							sp.stream.out_buf=tif.tif_rawdata;
							sp.stream.next_out=0;
							sp.stream.avail_out=tif.tif_rawdatasize;
						}
						break;
					default:
						TIFFErrorExt(tif.tif_clientdata, module, "{0}: zlib error: {1}", tif.tif_name, sp.stream.msg);
						return false;
				}
			} while(state!=zlib.Z_STREAM_END);
			return true;
		}

		static void ZIPCleanup(TIFF tif)
		{
			ZIPState sp=tif.tif_data as ZIPState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			TIFFPredictorCleanup(tif);

			tif.tif_tagmethods.vgetfield=sp.vgetparent;
			tif.tif_tagmethods.vsetfield=sp.vsetparent;

			if((sp.state&ZSTATE.INIT_ENCODE)==ZSTATE.INIT_ENCODE)
			{
				zlib.deflateEnd(sp.stream);
				sp.state=ZSTATE.None;
			}
			else if((sp.state&ZSTATE.INIT_DECODE)==ZSTATE.INIT_DECODE)
			{
				zlib.inflateEnd(sp.stream);
				sp.state=ZSTATE.None;
			}
			tif.tif_data=null;

			TIFFSetDefaultCompressionState(tif);
		}

		static bool ZIPVSetField(TIFF tif, TIFFTAG tag, TIFFDataType dt, object[] ap)
		{
			ZIPState sp=tif.tif_data as ZIPState;
			string module="ZIPVSetField";

			switch(tag)
			{
				case TIFFTAG.ZIPQUALITY:
					sp.zipquality=__GetAsInt(ap, 0);
					if((sp.state&ZSTATE.INIT_ENCODE)==ZSTATE.INIT_ENCODE)
					{
						if(zlib.deflateParams(sp.stream, sp.zipquality, zlib.Z_DEFAULT_STRATEGY)!=zlib.Z_OK)
						{
							TIFFErrorExt(tif.tif_clientdata, module, "{0}: zlib error: {1}", tif.tif_name, sp.stream.msg);
							return false;
						}
					}
					return true;
				default: return sp.vsetparent(tif, tag, dt, ap);
			}
			//NOTREACHED
		}

		static bool ZIPVGetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			ZIPState sp=tif.tif_data as ZIPState;
			switch(tag)
			{
				case TIFFTAG.ZIPQUALITY: ap[0]=sp.zipquality; break;
				default: return sp.vgetparent(tif, tag, ap);
			}
			return true;
		}

		static readonly TIFFFieldInfo zipFieldInfo =new TIFFFieldInfo(TIFFTAG.ZIPQUALITY, 0, 0, TIFFDataType.TIFF_ANY, FIELD.PSEUDO, true, false, "" );

		static bool TIFFInitZIP(TIFF tif, COMPRESSION scheme)
		{
			string module="TIFFInitZIP";

#if DEBUG
			if(scheme!=COMPRESSION.DEFLATE&&scheme!=COMPRESSION.ADOBE_DEFLATE) throw new Exception("scheme!=COMPRESSION.DEFLATE&&scheme!=COMPRESSION.ADOBE_DEFLATE");
#endif

			// Merge codec-specific tag information.
			if(!_TIFFMergeFieldInfo(tif, zipFieldInfo))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Merging Deflate codec-specific tags failed");
				return false;
			}
			
			// Allocate state block so tag methods have storage to record values.
			ZIPState sp=null;
			try
			{
				tif.tif_data=sp=new ZIPState();
				sp.stream=new zlib.z_stream();
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, module, "No space for ZIP state block");
				return false;
			}

			// Override parent get/set field methods.
			sp.vgetparent=tif.tif_tagmethods.vgetfield;
			tif.tif_tagmethods.vgetfield=ZIPVGetField; // hook for codec tags
			sp.vsetparent=tif.tif_tagmethods.vsetfield;
			tif.tif_tagmethods.vsetfield=ZIPVSetField; // hook for codec tags

			// Default values for codec-specific fields
			sp.zipquality=zlib.Z_DEFAULT_COMPRESSION; // default comp. level
			sp.state=ZSTATE.None;

			// Install codec methods.
			tif.tif_setupdecode=ZIPSetupDecode;
			tif.tif_predecode=ZIPPreDecode;
			tif.tif_decoderow=ZIPDecode;
			tif.tif_decodestrip=ZIPDecode;
			tif.tif_decodetile=ZIPDecode;
			tif.tif_setupencode=ZIPSetupEncode;
			tif.tif_preencode=ZIPPreEncode;
			tif.tif_postencode=ZIPPostEncode;
			tif.tif_encoderow=ZIPEncode;
			tif.tif_encodestrip=ZIPEncode;
			tif.tif_encodetile=ZIPEncode;
			tif.tif_cleanup=ZIPCleanup;

			// Setup predictor setup.
			TIFFPredictorInit(tif);
			return true;
		}
	}
}
#endif // ZIP_SUPORT
