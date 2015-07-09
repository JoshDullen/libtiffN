// tif_predict.cs
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
// Predictor Tag Support (used by multiple codecs).

using System;
using System.Collections.Generic;
using System.IO;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{	
		// "Library-private" Support for the Predictor Tag

		// Codecs that want to support the Predictor tag must place
		// this structure first in their private state block so that
		// the predictor code can cast tif_data to find its state.
		class TIFFPredictorState : ICodecState
		{
			internal PREDICTOR predictor;	// predictor tag value
			internal int stride;			// sample stride over data
			internal int rowsize;			// tile/strip row size

			internal TIFFCodeMethod encoderow;		// parent codec encode row
			internal TIFFCodeMethod encodestrip;	// parent codec encode strip
			internal TIFFCodeMethod encodetile;		// parent codec encode tile
			internal TIFFPostMethod encodepfunc;	// horizontal differencer

			internal TIFFCodeMethod decoderow;		// parent codec decode row
			internal TIFFCodeMethod decodestrip;	// parent codec decode strip
			internal TIFFCodeMethod decodetile;		// parent codec decode tile
			internal TIFFPostMethod decodepfunc;	// horizontal accumulator

			internal TIFFVGetMethod vgetparent;		// super-class method
			internal TIFFVSetMethod vsetparent;		// super-class method
			internal TIFFPrintMethod printdir;		// super-class method
			internal TIFFBoolMethod setupdecode;	// super-class method
			internal TIFFBoolMethod setupencode;	// super-class method
		}

		static bool PredictorSetup(TIFF tif)
		{
			string module="PredictorSetup";

			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
			TIFFDirectory td=tif.tif_dir;

			switch(sp.predictor)
			{
				case PREDICTOR.NONE: return true; // no differencing
				case PREDICTOR.HORIZONTAL:
					if(td.td_bitspersample!=8&&td.td_bitspersample!=16&&td.td_bitspersample!=32)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Horizontal differencing \"Predictor\" not supported with {0}-bit samples", td.td_bitspersample);
						return false;
					}
					break;
				case PREDICTOR.FLOATINGPOINT:
					if(td.td_sampleformat!=SAMPLEFORMAT.IEEEFP)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Floating point \"Predictor\" not supported with {0} data format", td.td_sampleformat);
						return false;
					}
					break;
				default:
					TIFFErrorExt(tif.tif_clientdata, module, "\"Predictor\" value {0} not supported", sp.predictor);
					return false;
			}

			sp.stride=(td.td_planarconfig==PLANARCONFIG.CONTIG?(int)td.td_samplesperpixel:1);

			// Calculate the scanline/tile-width size in bytes.
			if(isTiled(tif)) sp.rowsize=TIFFTileRowSize(tif);
			else sp.rowsize=TIFFScanlineSize(tif);

			return true;
		}

		static bool PredictorSetupDecode(TIFF tif)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
			TIFFDirectory td=tif.tif_dir;

			if(!sp.setupdecode(tif)||!PredictorSetup(tif)) return false;

			if(sp.predictor==PREDICTOR.HORIZONTAL)
			{
				switch(td.td_bitspersample)
				{
					case 8: sp.decodepfunc=horAcc8; break;
					case 16: sp.decodepfunc=horAcc16; break;
					case 32: sp.decodepfunc=horAcc32; break;
				}

				// Override default decoding method with one that does the
				// predictor stuff.
				if(tif.tif_decoderow!=PredictorDecodeRow)
				{
					sp.decoderow=tif.tif_decoderow;
					tif.tif_decoderow=PredictorDecodeRow;
					sp.decodestrip=tif.tif_decodestrip;
					tif.tif_decodestrip=PredictorDecodeTile;
					sp.decodetile=tif.tif_decodetile;
					tif.tif_decodetile=PredictorDecodeTile;
				}

				// If the data is horizontally differenced 16-bit data that
				// requires byte-swapping, then it must be byte swapped before
				// the accumulation step. We do this with a special-purpose
				// routine and override the normal post decoding logic that
				// the library setup when the directory was read.
				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)==TIF_FLAGS.TIFF_SWAB)
				{
					if(sp.decodepfunc==horAcc16)
					{
						sp.decodepfunc=swabHorAcc16;
						tif.tif_postdecode=TIFFNoPostDecode;
					}
					else if(sp.decodepfunc==horAcc32)
					{
						sp.decodepfunc=swabHorAcc32;
						tif.tif_postdecode=TIFFNoPostDecode;
					}
				}
			}
			else if(sp.predictor==PREDICTOR.FLOATINGPOINT)
			{
				sp.decodepfunc=fpAcc;

				// Override default decoding method with one that does the
				// predictor stuff.
				if(tif.tif_decoderow!=PredictorDecodeRow)
				{
					sp.decoderow=tif.tif_decoderow;
					tif.tif_decoderow=PredictorDecodeRow;
					sp.decodestrip=tif.tif_decodestrip;
					tif.tif_decodestrip=PredictorDecodeTile;
					sp.decodetile=tif.tif_decodetile;
					tif.tif_decodetile=PredictorDecodeTile;
				}

				// The data should not be swapped outside of the floating
				// point predictor, the accumulation routine should return
				// byres in the native order.
				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)==TIF_FLAGS.TIFF_SWAB) tif.tif_postdecode=TIFFNoPostDecode;

				// Allocate buffer to keep the decoded bytes before
				// rearranging in the ight order
			}

			return true;
		}

		static bool PredictorSetupEncode(TIFF tif)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
			TIFFDirectory td=tif.tif_dir;

			if(!sp.setupencode(tif)||!PredictorSetup(tif)) return false;

			if(sp.predictor==PREDICTOR.HORIZONTAL)
			{
				switch(td.td_bitspersample)
				{
					case 8: sp.encodepfunc=horDiff8; break;
					case 16: sp.encodepfunc=horDiff16; break;
					case 32: sp.encodepfunc=horDiff32; break;
				}

				// Override default encoding method with one that does the
				// predictor stuff.
				if(tif.tif_encoderow!=PredictorEncodeRow)
				{
					sp.encoderow=tif.tif_encoderow;
					tif.tif_encoderow=PredictorEncodeRow;
					sp.encodestrip=tif.tif_encodestrip;
					tif.tif_encodestrip=PredictorEncodeTile;
					sp.encodetile=tif.tif_encodetile;
					tif.tif_encodetile=PredictorEncodeTile;
				}
			}
			else if(sp.predictor==PREDICTOR.FLOATINGPOINT)
			{
				sp.encodepfunc=fpDiff;

				// Override default encoding method with one that does the
				// predictor stuff.
				if(tif.tif_encoderow!=PredictorEncodeRow)
				{
					sp.encoderow=tif.tif_encoderow;
					tif.tif_encoderow=PredictorEncodeRow;
					sp.encodestrip=tif.tif_encodestrip;
					tif.tif_encodestrip=PredictorEncodeTile;
					sp.encodetile=tif.tif_encodetile;
					tif.tif_encodetile=PredictorEncodeTile;
				}
			}

			return true;
		}

		unsafe static void horAcc8(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			int stride=((TIFFPredictorState)tif.tif_data).stride;

			if(cc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					byte* cp=cp0_+cp0_offset;
					cc-=stride;

					// Pipeline the most common cases.
					if(stride==3)
					{
						byte cr=cp[0];
						byte cg=cp[1];
						byte cb=cp[2];
						do
						{
							cc-=3;
							cp+=3;
							cp[0]=(cr+=cp[0]);
							cp[1]=(cg+=cp[1]);
							cp[2]=(cb+=cp[2]);
						}
						while(cc>0);
					}
					else if(stride==4)
					{
						byte cr=cp[0];
						byte cg=cp[1];
						byte cb=cp[2];
						byte ca=cp[3];
						do
						{
							cc-=4;
							cp+=4;
							cp[0]=(cr+=cp[0]);
							cp[1]=(cg+=cp[1]);
							cp[2]=(cb+=cp[2]);
							cp[3]=(ca+=cp[3]);
						}
						while(cc>0);
					}
					else
					{
						do
						{
							//was REPEAT4(stride, cp[stride]+=*(cp++))
							switch(stride)
							{
								default: for(int i=stride-4; i>0; i--) cp[stride]+=*(cp++); goto case 4;
								case 4: cp[stride]+=*(cp++); goto case 3;
								case 3: cp[stride]+=*(cp++); goto case 2;
								case 2: cp[stride]+=*(cp++); goto case 1;
								case 1: cp[stride]+=*(cp++); break;
								case 0: break;
							}

							cc-=stride;
						}
						while(cc>0);
					}
				}
			}
		}

		unsafe static void swabHorAcc16(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			int stride=((TIFFPredictorState)tif.tif_data).stride;
			int wc=cc/2;

			if(wc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					ushort* wp=(ushort*)(cp0_+cp0_offset);

					//was TIFFSwabArrayOfShort(wp, wc);
					byte* cp=cp0_;
					while((wc--)>0)
					{
						byte t=cp[1]; cp[1]=cp[0]; cp[0]=t;
						cp+=2;
					}

					wc-=stride;
					do
					{
						//was REPEAT4(stride, wp[stride]+=*(wp++));
						switch(stride)
						{
							default: for(int i=stride-4; i>0; i--) wp[stride]+=*(wp++); goto case 4;
							case 4: wp[stride]+=*(wp++); goto case 3;
							case 3: wp[stride]+=*(wp++); goto case 2;
							case 2: wp[stride]+=*(wp++); goto case 1;
							case 1: wp[stride]+=*(wp++); break;
							case 0: break;
						}

						wc-=stride;
					}
					while(wc>0);
				}
			}
		}

		unsafe static void horAcc16(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			int stride=((TIFFPredictorState)tif.tif_data).stride;
			int wc=cc/2;

			if(wc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					ushort* wp=(ushort*)(cp0_+cp0_offset);

					wc-=stride;
					do
					{
						//was REPEAT4(stride, wp[stride]+=*(wp++));
						switch(stride)
						{
							default: for(int i=stride-4; i>0; i--) wp[stride]+=*(wp++); goto case 4;
							case 4: wp[stride]+=*(wp++); goto case 3;
							case 3: wp[stride]+=*(wp++); goto case 2;
							case 2: wp[stride]+=*(wp++); goto case 1;
							case 1: wp[stride]+=*(wp++); break;
							case 0: break;
						}
						wc-=stride;
					}
					while(wc>0);
				}
			}
		}

		unsafe static void swabHorAcc32(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			int stride=((TIFFPredictorState)tif.tif_data).stride;
			int wc=cc/4;

			if(wc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					ushort* wp=(ushort*)(cp0_+cp0_offset);

					//was TIFFSwabArrayOfLong(wp, wc);
					byte* cp=cp0_;
					while((wc--)>0)
					{
						byte t=cp[3]; cp[3]=cp[0]; cp[0]=t;
						t=cp[2]; cp[2]=cp[1]; cp[1]=t;
						cp+=4;
					}

					wc-=stride;
					do
					{
						//was REPEAT4(stride, wp[stride]+=*(wp++));
						switch(stride)
						{
							default: for(int i=stride-4; i>0; i--) wp[stride]+=*(wp++); goto case 4;
							case 4: wp[stride]+=*(wp++); goto case 3;
							case 3: wp[stride]+=*(wp++); goto case 2;
							case 2: wp[stride]+=*(wp++); goto case 1;
							case 1: wp[stride]+=*(wp++); break;
							case 0: break;
						}

						wc-=stride;
					}
					while(wc>0);
				}
			}
		}

		unsafe static void horAcc32(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			int stride=((TIFFPredictorState)tif.tif_data).stride;
			int wc=cc/4;

			if(wc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					ushort* wp=(ushort*)(cp0_+cp0_offset);

					wc-=stride;
					do
					{
						//was REPEAT4(stride, wp[stride]+=*(wp++));
						switch(stride)
						{
							default: for(int i=stride-4; i>0; i--) wp[stride]+=*(wp++); goto case 4;
							case 4: wp[stride]+=*(wp++); goto case 3;
							case 3: wp[stride]+=*(wp++); goto case 2;
							case 2: wp[stride]+=*(wp++); goto case 1;
							case 1: wp[stride]+=*(wp++); break;
							case 0: break;
						}
						wc-=stride;
					}
					while(wc>0);
				}
			}
		}

		// Floating point predictor accumulation routine.
		unsafe static void fpAcc(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			int stride=((TIFFPredictorState)tif.tif_data).stride;
			int bps=tif.tif_dir.td_bitspersample/8;
			int wc=cc/bps;
			int count=cc;

			byte[] tmp=null;
			try
			{
				tmp=new byte[cc];
			}
			catch
			{
				return;
			}

			fixed(byte* cp0_=cp0)
			{
				byte* cp=cp0_+cp0_offset;

				while(count>stride)
				{
					//was REPEAT4(stride, cp[stride]+=*(cp++));
					switch(stride)
					{
						default: for(int i=stride-4; i>0; i--) cp[stride]+=*(cp++); goto case 4;
						case 4: cp[stride]+=*(cp++); goto case 3;
						case 3: cp[stride]+=*(cp++); goto case 2;
						case 2: cp[stride]+=*(cp++); goto case 1;
						case 1: cp[stride]+=*(cp++); break;
						case 0: break;
					}
					count-=stride;
				}

				Array.Copy(cp0, cp0_offset, tmp, 0, cc);
				cp=cp0_+cp0_offset;
				for(count=0; count<wc; count++)
				{
					for(uint b=0; b<bps; b++) cp[bps*count+b]=tmp[(bps-b-1)*wc+count];
				}
			}
		}

		// Decode a scanline and apply the predictor routine.
		static bool PredictorDecodeRow(TIFF tif, byte[] op0, int occ0, ushort s)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			if(sp.decoderow(tif, op0, occ0, s))
			{
				sp.decodepfunc(tif, op0, 0, occ0);
				return true;
			}

			return false;
		}

		// Decode a tile/strip and apply the predictor routine.
		// Note that horizontal differencing must be done on a
		// row-by-row basis. The width of a "row" has already
		// been calculated at pre-decode time according to the
		// strip/tile dimensions.
		static bool PredictorDecodeTile(TIFF tif, byte[] op0, int occ0, ushort s)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			if(sp.decodetile(tif, op0, occ0, s))
			{
				int rowsize=sp.rowsize;
#if DEBUG
				if(rowsize<=0) throw new Exception("rowsize<=0");
#endif
				int op0_offset=0;
				while(occ0>0)
				{
					sp.decodepfunc(tif, op0, op0_offset, rowsize);
					occ0-=rowsize;
					op0_offset+=rowsize;
				}
				return true;
			}

			return false;
		}

		unsafe static void horDiff8(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
			int stride=sp.stride;

			if(cc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					byte* cp=cp0_+cp0_offset;
					cc-=stride;

					// Pipeline the most common cases.
					if(stride==3)
					{
						byte r1, g1, b1;
						byte r2=cp[0];
						byte g2=cp[1];
						byte b2=cp[2];
						do
						{
							r1=cp[3]; cp[3]-=r2; r2=r1;
							g1=cp[4]; cp[4]-=g2; g2=g1;
							b1=cp[5]; cp[5]-=b2; b2=b1;
							cp+=3;
							cc-=3;
						}
						while(cc>0);
					}
					else if(stride==4)
					{
						byte r1, g1, b1, a1;
						byte r2=cp[0];
						byte g2=cp[1];
						byte b2=cp[2];
						byte a2=cp[3];
						do
						{
							r1=cp[4]; cp[4]-=r2; r2=r1;
							g1=cp[5]; cp[5]-=g2; g2=g1;
							b1=cp[6]; cp[6]-=b2; b2=b1;
							a1=cp[7]; cp[7]-=a2; a2=a1;
							cp+=4;
							cc-=4;
						}
						while(cc>0);
					}
					else
					{
						cp+=cc-1;
						do
						{
							//was REPEAT4(stride, cp[stride]-=*(cp--));
							switch(stride)
							{
								default: for(int i=stride-4; i>0; i--) cp[stride]-=*(cp--); goto case 4;
								case 4: cp[stride]-=*(cp--); goto case 3;
								case 3: cp[stride]-=*(cp--); goto case 2;
								case 2: cp[stride]-=*(cp--); goto case 1;
								case 1: cp[stride]-=*(cp--); break;
								case 0: break;
							}

							cc-=stride;
						}
						while(cc>0);
					}
				}
			}
		}

		unsafe static void horDiff16(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
			int stride=sp.stride;
			int wc=cc/2;

			if(wc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					ushort* wp=(ushort*)(cp0_+cp0_offset);

					wc-=stride;
					wp+=wc-1;
					do
					{
						//was REPEAT4(stride, wp[stride]-=*(wp--));
						switch(stride)
						{
							default: for(int i=stride-4; i>0; i--) wp[stride]-=*(wp--); goto case 4;
							case 4: wp[stride]-=*(wp--); goto case 3;
							case 3: wp[stride]-=*(wp--); goto case 2;
							case 2: wp[stride]-=*(wp--); goto case 1;
							case 1: wp[stride]-=*(wp--); break;
							case 0: break;
						}

						wc-=stride;
					}
					while(wc>0);
				}
			}
		}

		unsafe static void horDiff32(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
			int stride=sp.stride;
			int wc=cc/4;

			if(wc>stride)
			{
				fixed(byte* cp0_=cp0)
				{
					ushort* wp=(ushort*)(cp0_+cp0_offset);

					wc-=stride;
					wp+=wc-1;
					do
					{
						//was REPEAT4(stride, wp[stride]-=*(wp--));
						switch(stride)
						{
							default: for(int i=stride-4; i>0; i--) wp[stride]-=*(wp--); goto case 4;
							case 4: wp[stride]-=*(wp--); goto case 3;
							case 3: wp[stride]-=*(wp--); goto case 2;
							case 2: wp[stride]-=*(wp--); goto case 1;
							case 1: wp[stride]-=*(wp--); break;
							case 0: break;
						}

						wc-=stride;
					}
					while(wc>0);
				}
			}
		}

		// Floating point predictor differencing routine.
		unsafe static void fpDiff(TIFF tif, byte[] cp0, int cp0_offset, int cc)
		{
			int stride=((TIFFPredictorState)tif.tif_data).stride;
			int bps=tif.tif_dir.td_bitspersample/8;
			int wc=cc/bps;
			int count;

			byte[] tmp=null;
			try
			{
				tmp=new byte[cc];
			}
			catch
			{
				return;
			}

			Array.Copy(cp0, cp0_offset, tmp, 0, cc);

			fixed(byte* cp0_=cp0)
			{
				byte* cp=cp0_+cp0_offset;

				for(count=0; count<wc; count++)
				{
					for(int b=0; b<bps; b++)
					{
						cp[(bps-b-1)*wc+count]=tmp[bps*count+b];
					}
				}
				tmp=null;

				cp=cp0_+cp0_offset;
				cp+=cc-stride-1;
				for(count=cc; count>stride; count-=stride)
				{
					//was REPEAT4(stride, cp[stride]-=*(cp--));
					switch(stride)
					{
						default: for(int i=stride-4; i>0; i--) cp[stride]-=*(cp--); goto case 4;
						case 4: cp[stride]-=*(cp--); goto case 3;
						case 3: cp[stride]-=*(cp--); goto case 2;
						case 2: cp[stride]-=*(cp--); goto case 1;
						case 1: cp[stride]-=*(cp--); break;
						case 0: break;
					}
				}
			}
		}

		static bool PredictorEncodeRow(TIFF tif, byte[] bp, int cc, ushort s)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			// XXX horizontal differencing alters user's data XXX
			sp.encodepfunc(tif, bp, 0, cc);
			return sp.encoderow(tif, bp, cc, s);
		}

		static bool PredictorEncodeTile(TIFF tif, byte[] bp0, int cc0, ushort s)
		{
			string module="PredictorEncodeTile";
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
			int cc=cc0;

			byte[] bp=null;
			try
			{
				bp=new byte[cc0];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Out of memory allocating {0} byte temp buffer.", cc0);
				return false;
			}

			bp0.CopyTo(bp, 0);

			int rowsize=sp.rowsize;
#if DEBUG
			if(rowsize<=0) throw new Exception("rowsize<=0");
			if((cc0%rowsize)!=0) throw new Exception("(cc0%rowsize)!=0");
#endif

			int bp_offset=0;
			while(cc>0)
			{
				sp.encodepfunc(tif, bp, bp_offset, rowsize);
				cc-=rowsize;
				bp_offset+=rowsize;
			}

			return sp.encodetile(tif, bp, cc0, s);
		}

		static readonly TIFFFieldInfo predictFieldInfo=new TIFFFieldInfo(TIFFTAG.PREDICTOR, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CODEC, false, false, "Predictor");

		static bool PredictorVSetField(TIFF tif, TIFFTAG tag, TIFFDataType dt, params object[] ap)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			switch(tag)
			{
				case TIFFTAG.PREDICTOR:
					sp.predictor=(PREDICTOR)__GetAsUshort(ap, 0);
					TIFFSetFieldBit(tif, FIELD.CODEC);
					break;
				default: return sp.vsetparent(tif, tag, dt, ap);
			}
			tif.tif_flags|=TIF_FLAGS.TIFF_DIRTYDIRECT;

			return true;
		}

		static bool PredictorVGetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			switch(tag)
			{
				case TIFFTAG.PREDICTOR:
					ap[0]=(ushort)sp.predictor;
					break;
				default: return sp.vgetparent(tif, tag, ap);
			}
			return true;
		}

		static void PredictorPrintDir(TIFF tif, TextWriter fd, TIFFPRINT flags)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			if(TIFFFieldSet(tif, FIELD.CODEC))
			{
				fd.Write(" Predictor: ");
				switch(sp.predictor)
				{
					case PREDICTOR.NONE: fd.Write("none "); break;
					case PREDICTOR.HORIZONTAL: fd.Write("horizontal differencing "); break;
					case PREDICTOR.FLOATINGPOINT: fd.Write("floating point predictor "); break;
				}
				fd.WriteLine("{0} (0x{1:X2})\n", sp.predictor, sp.predictor);
			}
			if(sp.printdir!=null) sp.printdir(tif, fd, flags);
		}

		static bool TIFFPredictorInit(TIFF tif)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			// Merge codec-specific tag information.
			if(!_TIFFMergeFieldInfo(tif, predictFieldInfo))
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFPredictorInit", "Merging Predictor codec-specific tags failed");
				return false;
			}

			// Override parent get/set field methods.
			sp.vgetparent=tif.tif_tagmethods.vgetfield;
			tif.tif_tagmethods.vgetfield=PredictorVGetField;	// hook for predictor tag
			sp.vsetparent=tif.tif_tagmethods.vsetfield;
			tif.tif_tagmethods.vsetfield=PredictorVSetField;	// hook for predictor tag
			sp.printdir=tif.tif_tagmethods.printdir;
			tif.tif_tagmethods.printdir=PredictorPrintDir;		// hook for predictor tag

			sp.setupdecode=tif.tif_setupdecode;
			tif.tif_setupdecode=PredictorSetupDecode;
			sp.setupencode=tif.tif_setupencode;
			tif.tif_setupencode=PredictorSetupEncode;

			sp.predictor=PREDICTOR.NONE;	// default value
			sp.encodepfunc=null;			// no predictor routine
			sp.decodepfunc=null;			// no predictor routine
			return true;
		}

		static bool TIFFPredictorCleanup(TIFF tif)
		{
			TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;

			tif.tif_tagmethods.vgetfield=sp.vgetparent;
			tif.tif_tagmethods.vsetfield=sp.vsetparent;
			tif.tif_tagmethods.printdir=sp.printdir;
			tif.tif_setupdecode=sp.setupdecode;
			tif.tif_setupencode=sp.setupencode;

			return true;
		}
	}
}
