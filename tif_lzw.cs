#if LZW_SUPPORT
// tif_lzw.cs
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
// Rev 5.0 Lempel-Ziv & Welch Compression Support
//
// This code is derived from the compress program whose code is
// derived from software contributed to Berkeley by James A. Woods,
// derived from original work by Spencer Thomas and Joseph Orost.
//
// The original Berkeley copyright notice appears below in its entirety.

#define LZW_COMPAT		// include backwards compatibility code

// Each strip of data is supposed to be terminated by a CODE_EOI.
// If the following #define is included, the decoder will also
// check for end-of-strip w/o seeing this code. This makes the
// library more robust, but also slower.
#define LZW_CHECKEOS	// include checks for strips w/o EOI code

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// NB:	The 5.0 spec describes a different algorithm than Aldus
		//		implements. Specifically, Aldus does code length transitions
		//		one code earlier than should be done (for real LZW).
		//		Earlier versions of this library implemented the correct
		//		LZW algorithm, but emitted codes in a bit order opposite
		//		to the TIFF spec. Thus, to maintain compatibility w/ Aldus
		//		we interpret MSB-LSB ordered codes to be images written w/
		//		old versions of this library, but otherwise adhere to the
		//		Aldus "off by one" algorithm.
		//
		// Future revisions to the TIFF spec are expected to "clarify this issue".

		static int MAXCODE(int n) { return (1<<n)-1; }

		// The TIFF spec specifies that encoded bit
		// strings range from 9 to 12 bits.
		const int BITS_MIN=9;		// start with 9 bits
		const int BITS_MAX=12;		// max of 12 bit strings
		// predefined codes
		const int CODE_CLEAR=256;	// code to clear string table
		const int CODE_EOI=257;		// end-of-information code
		const int CODE_FIRST=258;	// first free code entry
		const int CODE_MAX=(1<<BITS_MAX)-1;
		const int HSIZE=9001;		// 91% occupancy
		const int HSHIFT=13-8;

#if LZW_COMPAT
		// NB: +1024 is for compatibility with old files
		const int CSIZE=CODE_MAX+1024;
#else
		const int CSIZE=CODE_MAX+1;
#endif

		// State block for each open TIFF file using LZW
		// compression/decompression. Note that the predictor
		// state block must be first in this data structure.
		class LZWBaseState : TIFFPredictorState
		{
			internal ushort nbits;	// # of bits/code
			internal ushort maxcode;	// maximum code for lzw_nbits
			internal ushort free_ent;	// next free entry in hash table
			internal int nextdata;	// next bits of i/o
			internal int nextbits;	// # of valid bits in lzw_nextdata

			internal O rw_mode;		// preserve rw_mode from init
		}

		class hash_t
		{
			internal int hash;
			internal ushort code;
		}

		// Decoding-specific state.
		class code_t
		{
			internal int next;
			internal ushort length;	// string len, including this token
			internal byte value;		// data value
			internal byte firstchar;	// first token of string
		}

		delegate bool decodeFunc(TIFF tif, byte[] buf, int cc, ushort s);

		class LZWCodecState : LZWBaseState
		{
			internal const int CHECK_GAP=10000;	// enc_ratio check interval

			// Decoding specific data
			internal int dec_nbitsmask;		// lzw_nbits 1 bits, right adjusted
			internal int dec_restart;			// restart count
#if LZW_CHECKEOS
			internal int dec_bitsleft;		// available bits in raw data
#endif
			internal decodeFunc dec_decode;	// regular or backwards compatible
			internal int dec_codep;			// current recognized code
			internal int dec_oldcodep;		// previously recognized code
			internal int dec_free_entp;		// next free entry
			internal int dec_maxcodep;		// max available entry
			internal code_t[] dec_codetab;	// kept separate for small machines

			// Encoding specific data
			internal int enc_oldcode;			// last code encountered
			internal int enc_checkpoint;		// point at which to clear table
			internal int enc_ratio;			// current compression ratio
			internal int enc_incount;			// (input) data bytes encoded
			internal int enc_outcount;		// encoded (output) bytes
			internal uint enc_rawlimit;		// bound on tif_rawdata buffer
			internal hash_t[] enc_hashtab;	// kept separate for small machines
		}

		// LZW Decoder.
		static bool LZWSetupDecode(TIFF tif)
		{
			LZWCodecState sp=tif.tif_data as LZWCodecState;
			string module=" LZWSetupDecode";

			if(sp==null)
			{
				// Allocate state block so tag methods have storage to record 
				// values.
				try
				{
					tif.tif_data=sp=new LZWCodecState();
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, "LZWPreDecode", "No space for LZW state block");
					return false;
				}

				sp.dec_codetab=null;
				sp.dec_decode=null;

				// Setup predictor setup.
				TIFFPredictorInit(tif);

				sp=tif.tif_data as LZWCodecState;
			}

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			if(sp.dec_codetab==null)
			{
				try
				{
					sp.dec_codetab=new code_t[CSIZE];
					for(int i=0; i<CSIZE; i++)
					{
						sp.dec_codetab[i]=new code_t();
						sp.dec_codetab[i].next=-1;
					}
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, module, "No space for LZW code table");
					return false;
				}

				// Pre-load the table.
				byte code=255;
				do
				{
					sp.dec_codetab[code].value=code;
					sp.dec_codetab[code].firstchar=code;
					sp.dec_codetab[code].length=1;
					sp.dec_codetab[code].next=-1;
				} while((code--)!=0);

				// Zero-out the unused entries
				for(int i=CODE_CLEAR; i<CODE_FIRST; i++)
				{
					sp.dec_codetab[i].next=0;
					sp.dec_codetab[i].length=0;
					sp.dec_codetab[i].value=0;
					sp.dec_codetab[i].firstchar=0;
				}
			}
			return true;
		}

		// Setup state for decoding a strip.
		static bool LZWPreDecode(TIFF tif, ushort sampleNumber)
		{
			LZWCodecState sp=tif.tif_data as LZWCodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif
			if(sp.dec_codetab==null)
			{
				tif.tif_setupdecode(tif);
			}

			// Check for old bit-reversed codes.
			if(tif.tif_rawdata[0]==0&&(tif.tif_rawdata[1]&0x1)!=0)
			{
#if LZW_COMPAT
				if(sp.dec_decode==null)
				{
					TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "Old-style LZW codes, convert file");

					// Override default decoding methods with
					// ones that deal with the old coding.
					// Otherwise the predictor versions set
					// above will call the compatibility routines
					// through the dec_decode method.
					tif.tif_decoderow=LZWDecodeCompat;
					tif.tif_decodestrip=LZWDecodeCompat;
					tif.tif_decodetile=LZWDecodeCompat;

					// If doing horizontal differencing, must
					// re-setup the predictor logic since we
					// switched the basic decoder methods...
					tif.tif_setupdecode(tif);
					sp.dec_decode=LZWDecodeCompat;
				}

				sp.maxcode=(ushort)MAXCODE(BITS_MIN);
#else // !LZW_COMPAT
				if(sp.dec_decode==null)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Old-style LZW codes not supported");
					sp.dec_decode=LZWDecode;
				}
				return false;
#endif // !LZW_COMPAT
			}
			else
			{
				sp.maxcode=(ushort)(MAXCODE(BITS_MIN)-1);
				sp.dec_decode=LZWDecode;
			}
			sp.nbits=BITS_MIN;
			sp.nextbits=0;
			sp.nextdata=0;

			sp.dec_restart=0;
			sp.dec_nbitsmask=MAXCODE(BITS_MIN);
#if LZW_CHECKEOS
			sp.dec_bitsleft=(int)(tif.tif_rawcc<<3);
#endif
			sp.dec_free_entp=CODE_FIRST;

			// Zero entries that are not yet filled in. We do
			// this to guard against bogus input data that causes
			// us to index into undefined entries. If you can
			// come up with a way to safely bounds-check input codes
			// while decoding then you can remove this operation.
			for(int i=sp.dec_free_entp; i<CSIZE; i++)
			{
				sp.dec_codetab[i].firstchar=0;
				sp.dec_codetab[i].length=0;
				sp.dec_codetab[i].value=0;
				sp.dec_codetab[i].next=-1;
			}

			sp.dec_oldcodep=-1;
			sp.dec_maxcodep=sp.dec_nbitsmask-1;
			return true;
		}

		// Decode a "hunk of data".
		static void codeLoop(TIFF tif, string module)
		{
			TIFFErrorExt(tif.tif_clientdata, module, "LZWDecode: Bogus encoding, loop in the code table; scanline {0}", tif.tif_row);
		}

		static bool LZWDecode(TIFF tif, byte[] op0, int occ0, ushort s)
		{
			string module="LZWDecode";
			LZWCodecState sp=tif.tif_data as LZWCodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if(sp.dec_codetab==null) throw new Exception("sp.dec_codetab==null");
#endif

			unsafe
			{
				fixed(byte* op_=op0)
				{
					byte* op=op_;
					int occ=occ0;
					byte* tp;

					int codep;

					// Restart interrupted output operation.
					if(sp.dec_restart!=0)
					{
						int residue;

						codep=sp.dec_codep;
						residue=sp.dec_codetab[codep].length-sp.dec_restart;
						if(residue>occ)
						{
							// Residue from previous decode is sufficient
							// to satisfy decode request. Skip to the
							// start of the decoded string, place decoded
							// values in the output buffer, and return.
							sp.dec_restart+=occ;
							do
							{
								codep=sp.dec_codetab[codep].next;
								residue--;
							} while(residue>occ&&codep!=-1);

							if(codep!=-1)
							{
								tp=op+occ;
								do
								{
									tp--;
									*tp=sp.dec_codetab[codep].value;
									codep=sp.dec_codetab[codep].next;
									occ--;
								} while(occ!=0&&codep!=-1);
							}
							return true;
						}

						// Residue satisfies only part of the decode request.
						op+=residue;
						occ-=residue;
						tp=op;
						do
						{
							tp--;
							*tp=sp.dec_codetab[codep].value;
							codep=sp.dec_codetab[codep].next;
							residue--;
						} while(residue!=0&&codep!=-1);
						sp.dec_restart=0;
					}

					ushort code;

					uint bp=tif.tif_rawcp;
					int nbits=sp.nbits;
					int nextdata=sp.nextdata;
					int nextbits=sp.nextbits;
					int nbitsmask=sp.dec_nbitsmask;
					int oldcodep=sp.dec_oldcodep;
					int free_entp=sp.dec_free_entp;
					int maxcodep=sp.dec_maxcodep;

					while(occ>0)
					{
#if LZW_CHECKEOS
						// This check shouldn't be necessary because each
						// strip is suppose to be terminated with CODE_EOI.
						if(sp.dec_bitsleft<nbits)
						{
							TIFFWarningExt(tif.tif_clientdata, module, "Strip {0} not terminated with EOI code", tif.tif_curstrip);
							code=CODE_EOI;
						}
						else
						{
							nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
							nextbits+=8;
							if(nextbits<nbits)
							{
								nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
								nextbits+=8;
							}
							code=(ushort)((nextdata>>(nextbits-nbits))&nbitsmask);
							nextbits-=nbits;

							sp.dec_bitsleft-=nbits;
						}
#else
						nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
						nextbits+=8;
						if(nextbits<nbits)
						{
							nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
							nextbits+=8;
						}
						code=(ushort)((nextdata>>(nextbits-nbits))&nbitsmask);
						nextbits-=nbits;
#endif

						if(code==CODE_EOI) break;

						if(code==CODE_CLEAR)
						{
							free_entp=CODE_FIRST;
							for(int i=CODE_FIRST; i<CSIZE; i++)
							{
								sp.dec_codetab[i].next=0;
								sp.dec_codetab[i].length=0;
								sp.dec_codetab[i].value=0;
								sp.dec_codetab[i].firstchar=0;
							}

							nbits=BITS_MIN;
							nbitsmask=MAXCODE(BITS_MIN);
							maxcodep=nbitsmask-1;

#if LZW_CHECKEOS
							// This check shouldn't be necessary because each
							// strip is suppose to be terminated with CODE_EOI.
							if(sp.dec_bitsleft<nbits)
							{
								TIFFWarningExt(tif.tif_clientdata, module, "Strip {0} not terminated with EOI code", tif.tif_curstrip);
								code=CODE_EOI;
							}
							else
							{
								nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
								nextbits+=8;
								if(nextbits<nbits)
								{
									nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
									nextbits+=8;
								}
								code=(ushort)((nextdata>>(nextbits-nbits))&nbitsmask);
								nextbits-=nbits;

								sp.dec_bitsleft-=nbits;
							}
#else
							nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
							nextbits+=8;
							if(nextbits<nbits)
							{
								nextdata=(nextdata<<8)|tif.tif_rawdata[bp++];
								nextbits+=8;
							}
							code=(ushort)((nextdata>>(nextbits-nbits))&nbitsmask);
							nextbits-=nbits;
#endif

							if(code==CODE_EOI) break;

							if(code==CODE_CLEAR)
							{
								TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "LZWDecode: Corrupted LZW table at scanline {0}", tif.tif_row);
								return false;
							}

							*op++=(byte)code;
							occ--;
							oldcodep=code;
							continue;
						}

						codep=code;

						// Add the new entry to the code table.
						if(free_entp<0||free_entp>=CSIZE)
						{
							TIFFErrorExt(tif.tif_clientdata, module, "Corrupted LZW table at scanline {0}", tif.tif_row);
							return false;
						}

						sp.dec_codetab[free_entp].next=oldcodep;
						if(oldcodep<0||oldcodep>=CSIZE)
						{
							TIFFErrorExt(tif.tif_clientdata, module, "Corrupted LZW table at scanline {0}", tif.tif_row);
							return false;
						}
						sp.dec_codetab[free_entp].firstchar=sp.dec_codetab[oldcodep].firstchar;
						sp.dec_codetab[free_entp].length=(ushort)(sp.dec_codetab[oldcodep].length+1);
						sp.dec_codetab[free_entp].value=(codep<free_entp)?sp.dec_codetab[codep].firstchar:sp.dec_codetab[free_entp].firstchar;

						free_entp++;
						if(free_entp>maxcodep)
						{
							nbits++;
							if(nbits>BITS_MAX) nbits=BITS_MAX; // should not happen

							nbitsmask=MAXCODE(nbits);
							maxcodep=nbitsmask-1;
						}

						oldcodep=codep;
						if(code>=256)
						{
							// Code maps to a string, copy string
							// value to output (written in reverse).
							if(sp.dec_codetab[codep].length==0)
							{
								TIFFErrorExt(tif.tif_clientdata, module, "Wrong length of decoded string: data probably corrupted at scanline {0}", tif.tif_row);
								return false;
							}

							if(sp.dec_codetab[codep].length>occ)
							{
								// String is too long for decode buffer,
								// locate portion that will fit, copy to
								// the decode buffer, and setup restart
								// logic for the next decoding call.
								sp.dec_codep=codep;
								do
								{
									codep=sp.dec_codetab[codep].next;
								} while(codep!=-1&&sp.dec_codetab[codep].length>occ);

								if(codep!=-1)
								{
									sp.dec_restart=occ;
									tp=op+occ;
									do
									{
										tp--;
										*tp=sp.dec_codetab[codep].value;
										codep=sp.dec_codetab[codep].next;
										occ--;
									} while(occ!=0&&codep!=-1);

									if(codep!=-1) codeLoop(tif, module);
								}
								break;
							}

							int len=sp.dec_codetab[codep].length;
							tp=op+len;

							do
							{
								tp--;
								*tp=sp.dec_codetab[codep].value;
								codep=sp.dec_codetab[codep].next;
							} while(codep!=-1&&tp>op);

							if(codep!=-1)
							{
								codeLoop(tif, module);
								break;
							}
#if DEBUG
							if(occ<len) throw new Exception("occ<len");
#endif
							op+=len;
							occ-=len;
						}
						else
						{
							*op++=(byte)code; occ--;
						}
					}

					tif.tif_rawcp=bp;
					sp.nbits=(ushort)nbits;
					sp.nextdata=nextdata;
					sp.nextbits=nextbits;
					sp.dec_nbitsmask=nbitsmask;
					sp.dec_oldcodep=oldcodep;
					sp.dec_free_entp=free_entp;
					sp.dec_maxcodep=maxcodep;

					if(occ>0)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Not enough data at scanline {0} (short {1} bytes)", tif.tif_row, occ);
						return false;
					}
				}
			}

			return true;
		}

#if LZW_COMPAT
		// Decode a "hunk of data" for old images.
		static bool LZWDecodeCompat(TIFF tif, byte[] op0, int occ0, ushort s)
		{
			string module="LZWDecodeCompat";
			LZWCodecState sp=tif.tif_data as LZWCodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			unsafe
			{
				fixed(byte* op_=op0)
				{
					byte* op=op_;
					int occ=occ0;
					byte* tp;
					int codep;

					// Restart interrupted output operation.
					if(sp.dec_restart!=0)
					{
						int residue;

						codep=sp.dec_codep;
						residue=sp.dec_codetab[codep].length-sp.dec_restart;
						if(residue>occ)
						{
							// Residue from previous decode is sufficient
							// to satisfy decode request. Skip to the
							// start of the decoded string, place decoded
							// values in the output buffer, and return.
							sp.dec_restart+=occ;
							do
							{
								codep=sp.dec_codetab[codep].next;
								residue--;
							} while(residue>occ);

							tp=op+occ;
							do
							{
								tp--;
								*tp=sp.dec_codetab[codep].value;
								codep=sp.dec_codetab[codep].next;
								occ--;
							} while(occ!=0);
							return true;
						}

						// Residue satisfies only part of the decode request.
						op+=residue;
						occ-=residue;
						tp=op;
						do
						{
							tp--;
							*tp=sp.dec_codetab[codep].value;
							codep=sp.dec_codetab[codep].next;
							residue--;
						} while(residue!=0);
						sp.dec_restart=0;
					}

					uint bp=tif.tif_rawcp;
					int nbits=sp.nbits;
					int nextdata=sp.nextdata;
					int nextbits=sp.nextbits;
					int nbitsmask=sp.dec_nbitsmask;
					int oldcodep=sp.dec_oldcodep;
					int free_entp=sp.dec_free_entp;
					int maxcodep=sp.dec_maxcodep;

					int code;
					while(occ>0)
					{
#if LZW_CHECKEOS
						// This check shouldn't be necessary because each
						// strip is suppose to be terminated with CODE_EOI.
						if(sp.dec_bitsleft<nbits)
						{
							TIFFWarningExt(tif.tif_clientdata, module, "Strip {0} not terminated with EOI code", tif.tif_curstrip);
							code=CODE_EOI;
						}
						else
						{
							nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
							nextbits+=8;
							if(nextbits<nbits)
							{
								nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
								nextbits+=8;
							}
							code=(ushort)(nextdata&nbitsmask);
							nextdata>>=nbits;
							nextbits-=nbits;

							sp.dec_bitsleft-=nbits;
						}
#else
						nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
						nextbits+=8;
						if(nextbits<nbits)
						{
							nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
							nextbits+=8;
						}
						code=(ushort)(nextdata&nbitsmask);
						nextdata>>=nbits;
						nextbits-=nbits;
#endif

						if(code==CODE_EOI) break;
						if(code==CODE_CLEAR)
						{
							free_entp=CODE_FIRST;
							for(int i=CODE_FIRST; i<CSIZE; i++)
							{
								sp.dec_codetab[i].next=0;
								sp.dec_codetab[i].length=0;
								sp.dec_codetab[i].value=0;
								sp.dec_codetab[i].firstchar=0;
							}
							nbits=BITS_MIN;
							nbitsmask=MAXCODE(BITS_MIN);
							maxcodep=nbitsmask;

#if LZW_CHECKEOS
							// This check shouldn't be necessary because each
							// strip is suppose to be terminated with CODE_EOI.
							if(sp.dec_bitsleft<nbits)
							{
								TIFFWarningExt(tif.tif_clientdata, module, "Strip {0} not terminated with EOI code", tif.tif_curstrip);
								code=CODE_EOI;
							}
							else
							{
								nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
								nextbits+=8;
								if(nextbits<nbits)
								{
									nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
									nextbits+=8;
								}
								code=(ushort)(nextdata&nbitsmask);
								nextdata>>=nbits;
								nextbits-=nbits;

								sp.dec_bitsleft-=nbits;
							}
#else
							nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
							nextbits+=8;
							if(nextbits<nbits)
							{
								nextdata|=(int)(((uint)tif.tif_rawdata[bp++])<<nextbits);
								nextbits+=8;
							}
							code=(ushort)(nextdata&nbitsmask);
							nextdata>>=nbits;
							nextbits-=nbits;
#endif

							if(code==CODE_EOI) break;
							if(code==CODE_CLEAR)
							{
								TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "LZWDecode: Corrupted LZW table at scanline {0}", tif.tif_row);
								return false;
							}

							*op++=(byte)code;
							occ--;
							oldcodep=code;
							continue;
						}
						codep=code;

						// Add the new entry to the code table.
						if(free_entp<0||free_entp>=CSIZE)
						{
							TIFFErrorExt(tif.tif_clientdata, module, "Corrupted LZW table at scanline {0}", tif.tif_row);
							return false;
						}

						sp.dec_codetab[free_entp].next=oldcodep;
						if(oldcodep<0||oldcodep>=CSIZE)
						{
							TIFFErrorExt(tif.tif_clientdata, module, "Corrupted LZW table at scanline {0}", tif.tif_row);
							return false;
						}

						sp.dec_codetab[free_entp].firstchar=sp.dec_codetab[oldcodep].firstchar;
						sp.dec_codetab[free_entp].length=(ushort)(sp.dec_codetab[oldcodep].length+1);
						sp.dec_codetab[free_entp].value=(codep<free_entp)?sp.dec_codetab[codep].firstchar:sp.dec_codetab[free_entp].firstchar;

						free_entp++;
						if(free_entp>maxcodep)
						{
							nbits++;
							if(nbits>BITS_MAX) nbits=BITS_MAX; // should not happen

							nbitsmask=MAXCODE(nbits);
							maxcodep=nbitsmask;
						}

						oldcodep=codep;
						if(code>=256)
						{
							byte* op_orig=op;

							// Code maps to a string, copy string
							// value to output (written in reverse).
							if(sp.dec_codetab[codep].length==0)
							{
								TIFFErrorExt(tif.tif_clientdata, module, "Wrong length of decoded string: data probably corrupted at scanline {0}", tif.tif_row);
								return false;
							}
							if(sp.dec_codetab[codep].length>occ)
							{
								// String is too long for decode buffer,
								// locate portion that will fit, copy to
								// the decode buffer, and setup restart
								// logic for the next decoding call.
								sp.dec_codep=codep;
								do
								{
									codep=sp.dec_codetab[codep].next;
								} while(sp.dec_codetab[codep].length>occ);
								sp.dec_restart=occ;
								tp=op+occ;

								do
								{
									tp--;
									*tp=sp.dec_codetab[codep].value;
									codep=sp.dec_codetab[codep].next;
									occ--;
								} while(occ!=0);
								break;
							}

#if DEBUG
							if(occ<sp.dec_codetab[codep].length) throw new Exception("occ<sp.dec_codetab[codep].length");
#endif
							op+=sp.dec_codetab[codep].length; occ-=sp.dec_codetab[codep].length;
							tp=op;
							do
							{
								tp--;
								*tp=sp.dec_codetab[codep].value;
								codep=sp.dec_codetab[codep].next;
							} while((codep!=-1)&&(tp>op_orig));
						}
						else
						{
							*op++=(byte)code; occ--;
						}
					}

					tif.tif_rawcp=bp;
					sp.nbits=(ushort)nbits;
					sp.nextdata=nextdata;
					sp.nextbits=nextbits;
					sp.dec_nbitsmask=nbitsmask;
					sp.dec_oldcodep=oldcodep;
					sp.dec_free_entp=free_entp;
					sp.dec_maxcodep=maxcodep;

					if(occ>0)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Not enough data at scanline {0} (short {1} bytes)", tif.tif_row, occ);
						return false;
					}
				}
			}

			return true;
		}
#endif // LZW_COMPAT

		// LZW Encoding.
		static bool LZWSetupEncode(TIFF tif)
		{
			LZWCodecState sp=tif.tif_data as LZWCodecState;
			string module="LZWSetupEncode";

#if DEBUG
			if(sp==null) throw new Exception("sp=null");
#endif

			try
			{
				sp.enc_hashtab=new hash_t[HSIZE];
				for(int i=0; i<HSIZE; i++) sp.enc_hashtab[i]=new hash_t();
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, module, "No space for LZW hash table");
				return false;
			}
			return true;
		}

		// Reset encoding state at the start of a strip.
		static bool LZWPreEncode(TIFF tif, ushort sampleNumber)
		{
			LZWCodecState sp=tif.tif_data as LZWCodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp=null");
#endif

			if(sp.enc_hashtab==null)
			{
				tif.tif_setupencode(tif);
			}

			sp.nbits=BITS_MIN;
			sp.maxcode=(ushort)MAXCODE(BITS_MIN);
			sp.free_ent=CODE_FIRST;
			sp.nextbits=0;
			sp.nextdata=0;
			sp.enc_checkpoint=LZWCodecState.CHECK_GAP;
			sp.enc_ratio=0;
			sp.enc_incount=0;
			sp.enc_outcount=0;
			
			// The 4 here insures there is space for 2 max-sized
			// codes in LZWEncode and LZWPostDecode.
			sp.enc_rawlimit=tif.tif_rawdatasize-1-4;
			cl_hash(sp);		// clear hash table
			sp.enc_oldcode=-1;	// generates CODE_CLEAR in LZWEncode
			return true;
		}

		// Encode a chunk of pixels.
		//
		// Uses an open addressing double hashing (no chaining) on the 
		// prefix code/next character combination. We do a variant of
		// Knuth's algorithm D (vol. 3, sec. 6.4) along with G. Knott's
		// relatively-prime secondary probe. Here, the modular division
		// first probe is gives way to a faster exclusive-or manipulation. 
		// Also do block compression with an adaptive reset, whereby the
		// code table is cleared when the compression ratio decreases,
		// but after the table fills The variable-length output codes
		// are re-sized at this point, and a CODE_CLEAR is generated
		// for the decoder.
		static bool LZWEncode(TIFF tif, byte[] buf, int cc, ushort s)
		{
			LZWCodecState sp=tif.tif_data as LZWCodecState;
			if(sp==null) return false;

#if DEBUG
			if(sp.enc_hashtab==null) throw new Exception("sp.enc_hashtab==null");
#endif

			unsafe
			{
				fixed(byte* bp_=buf)
				{
					byte* bp=bp_;

					// Load local state.
					int incount=sp.enc_incount;
					int outcount=sp.enc_outcount;
					int checkpoint=sp.enc_checkpoint;
					int nextdata=sp.nextdata;
					int nextbits=sp.nextbits;
					int free_ent=sp.free_ent;
					int maxcode=sp.maxcode;
					int nbits=sp.nbits;
					uint op=tif.tif_rawcp;
					uint limit=sp.enc_rawlimit;
					int ent=sp.enc_oldcode;

					if(ent==-1&&cc>0)
					{
						// NB:	This is safe because it can only happen
						//		at the start of a strip where we know there
						//		is space in the data buffer.
						nextdata=(nextdata<<nbits)|CODE_CLEAR;
						nextbits+=nbits;
						tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
						nextbits-=8;
						if(nextbits>=8)
						{
							tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
							nextbits-=8;
						}
						outcount+=nbits;

						ent=*bp++;
						cc--;
						incount++;
					}

					int disp;

					while(cc>0)
					{
						int c=*bp++; cc--; incount++;
						int fcode=((int)c<<BITS_MAX)+ent;
						int h=(c<<HSHIFT)^ent;	// xor hashing

						// Check hash index for an overflow.
						if(h>=HSIZE) h-=HSIZE;

						hash_t hp=sp.enc_hashtab[h];
						if(hp.hash==fcode)
						{
							ent=hp.code;
							continue;
						}

						if(hp.hash>=0)
						{
							// Primary hash failed, check secondary hash.
							disp=HSIZE-h;
							if(h==0) disp=1;
							do
							{
								// Avoid pointer arithmetic 'cuz of
								// wraparound problems with segments.
								if((h-=disp)<0) h+=HSIZE;
								hp=sp.enc_hashtab[h];
								if(hp.hash==fcode)
								{
									ent=hp.code;
									break;
								}
							} while(hp.hash>=0);

							if(hp.hash==fcode) continue;
						}

						// New entry, emit code and add to table.

						// Verify there is space in the buffer for the code
						// and any potential Clear code that might be emitted
						// below. The value of limit is setup so that there
						// are at least 4 bytes free--room for 2 codes.
						if(op>limit)
						{
							tif.tif_rawcc=op;
							TIFFFlushData1(tif);
							op=0;
						}

						nextdata=(nextdata<<nbits)|ent;
						nextbits+=nbits;
						tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
						nextbits-=8;
						if(nextbits>=8)
						{
							tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
							nextbits-=8;
						}
						outcount+=nbits;

						ent=c;
						hp.code=(ushort)(free_ent++);
						hp.hash=fcode;
						if(free_ent==CODE_MAX-1)
						{
							// table is full, emit clear code and reset
							cl_hash(sp);
							sp.enc_ratio=0;
							incount=0;
							outcount=0;
							free_ent=CODE_FIRST;

							nextdata=(nextdata<<nbits)|CODE_CLEAR;
							nextbits+=nbits;
							tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
							nextbits-=8;
							if(nextbits>=8)
							{
								tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
								nextbits-=8;
							}
							outcount+=nbits;

							nbits=BITS_MIN;
							maxcode=MAXCODE(BITS_MIN);
						}
						else
						{
							// If the next entry is going to be too big for
							// the code size, then increase it, if possible.
							if(free_ent>maxcode)
							{
								nbits++;

#if DEBUG
								if(nbits>BITS_MAX) throw new Exception("nbits>BITS_MAX");
#endif

								maxcode=MAXCODE(nbits);
							}
							else if(incount>=checkpoint)
							{
								// Check compression ratio and, if things seem
								// to be slipping, clear the hash table and
								// reset state. The compression ratio is a
								// 24+8-bit fractional number.
								checkpoint=incount+LZWCodecState.CHECK_GAP;

								int rat;
								if(incount>0x007fffff) // NB: shift will overflow
								{
									rat=outcount>>8;
									rat=(rat==0?0x7fffffff:incount/rat);
								}
								else rat=(incount<<8)/outcount;

								if(rat<=sp.enc_ratio)
								{
									cl_hash(sp);
									sp.enc_ratio=0;
									incount=0;
									outcount=0;
									free_ent=CODE_FIRST;

									nextdata=(nextdata<<nbits)|CODE_CLEAR;
									nextbits+=nbits;
									tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
									nextbits-=8;
									if(nextbits>=8)
									{
										tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
										nextbits-=8;
									}
									outcount+=nbits;

									nbits=BITS_MIN;
									maxcode=MAXCODE(BITS_MIN);
								}
								else sp.enc_ratio=rat;
							}
						}
					}

					// Restore global state.
					sp.enc_incount=incount;
					sp.enc_outcount=outcount;
					sp.enc_checkpoint=checkpoint;
					sp.enc_oldcode=ent;
					sp.nextdata=nextdata;
					sp.nextbits=nextbits;
					sp.free_ent=(ushort)free_ent;
					sp.maxcode=(ushort)maxcode;
					sp.nbits=(ushort)nbits;
					tif.tif_rawcp=op;
				}
			}

			return true;
		}

		// Finish off an encoded strip by flushing the last
		// string and tacking on an End Of Information code.
		static bool LZWPostEncode(TIFF tif)
		{
			LZWCodecState sp=tif.tif_data as LZWCodecState;

			uint op=tif.tif_rawcp;
			int nextbits=sp.nextbits;
			int nextdata=sp.nextdata;
			int outcount=sp.enc_outcount;
			int nbits=sp.nbits;

			if(op>sp.enc_rawlimit)
			{
				tif.tif_rawcc=op;
				TIFFFlushData1(tif);
				op=0;
			}

			if(sp.enc_oldcode!=-1)
			{
				nextdata=(nextdata<<nbits)|sp.enc_oldcode;
				nextbits+=nbits;
				tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
				nextbits-=8;
				if(nextbits>=8)
				{
					tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
					nextbits-=8;
				}
				outcount+=nbits;

				sp.enc_oldcode=-1;
			}

			nextdata=(nextdata<<nbits)|CODE_EOI;
			nextbits+=nbits;
			tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
			nextbits-=8;
			if(nextbits>=8)
			{
				tif.tif_rawdata[op++]=(byte)(nextdata>>(nextbits-8));
				nextbits-=8;
			}
			outcount+=nbits;

			if(nextbits>0) tif.tif_rawdata[op++]=(byte)(nextdata<<(8-nextbits));
			tif.tif_rawcc=op;

			return true;
		}

		// Reset encoding hash table.
		static void cl_hash(LZWCodecState sp)
		{
			for(int i=0; i<HSIZE; i++) sp.enc_hashtab[i].hash=-1;
		}

		static void LZWCleanup(TIFF tif)
		{
			TIFFPredictorCleanup(tif);

			LZWCodecState sp=tif.tif_data as LZWCodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp=null");
#endif

			sp.dec_codetab=null;
			sp.enc_hashtab=null;
			tif.tif_data=null;

			TIFFSetDefaultCompressionState(tif);
		}

		static bool TIFFInitLZW(TIFF tif, COMPRESSION scheme)
		{
#if DEBUG
			if(scheme!=COMPRESSION.LZW) throw new Exception("scheme!=COMPRESSION.LZW");
#endif
			// Allocate state block so tag methods have storage to record values.
			LZWCodecState sp=null;
			try
			{
				tif.tif_data=sp=new LZWCodecState();
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFInitLZW", "No space for LZW state block");
				return false;
			}

			sp.dec_codetab=null;
			sp.dec_decode=null;
			sp.enc_hashtab=null;
			sp.rw_mode=tif.tif_mode;

			// Install codec methods.
			tif.tif_setupdecode=LZWSetupDecode;
			tif.tif_predecode=LZWPreDecode;
			tif.tif_decoderow=LZWDecode;
			tif.tif_decodestrip=LZWDecode;
			tif.tif_decodetile=LZWDecode;
			tif.tif_setupencode=LZWSetupEncode;
			tif.tif_preencode=LZWPreEncode;
			tif.tif_postencode=LZWPostEncode;
			tif.tif_encoderow=LZWEncode;
			tif.tif_encodestrip=LZWEncode;
			tif.tif_encodetile=LZWEncode;
			tif.tif_cleanup=LZWCleanup;

			// Setup predictor setup.
			TIFFPredictorInit(tif);
			return true;
		}
	}
}

// Copyright (c) 1985, 1986 The Regents of the University of California.
// All rights reserved.
//
// This code is derived from software contributed to Berkeley by
// James A. Woods, derived from original work by Spencer Thomas
// and Joseph Orost.
//
// Redistribution and use in source and binary forms are permitted
// provided that the above copyright notice and this paragraph are
// duplicated in all such forms and that any documentation,
// advertising materials, and other materials related to such
// distribution and use acknowledge that the software was developed
// by the University of California, Berkeley. The name of the
// University may not be used to endorse or promote products derived
// from this software without specific prior written permission.
// THIS SOFTWARE IS PROVIDED ``AS IS'' AND WITHOUT ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, WITHOUT LIMITATION, THE IMPLIED
// WARRANTIES OF MERCHANTIBILITY AND FITNESS FOR A PARTICULAR PURPOSE.

#endif // LZW_SUPPORT
