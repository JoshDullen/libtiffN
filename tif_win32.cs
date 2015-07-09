// tif_win32.cs
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

// TIFF Library Win32-specific Routines. Adapted from tif_unix.c 4/5/95 by
// Scott Wagner (wagner@itek.com), Itek Graphix, Rochester, NY USA

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		static int tiffReadProc(Stream fd, byte[] buf, int size)
		{
			try
			{
				return fd.Read(buf, 0, size);
			}
			catch
			{
				return 0;
			}
		}

		static int tiffWriteProc(Stream fd, byte[] buf, int size)
		{
			try
			{
				fd.Write(buf, 0, size);
				return size;
			}
			catch
			{
				return 0;
			}
		}

		static uint tiffSeekProc(Stream fd, long off, SEEK whence)
		{
			SeekOrigin dwMoveMethod=SeekOrigin.Begin;
			switch(whence)
			{
				case SEEK.SET: dwMoveMethod=SeekOrigin.Begin; break;
				case SEEK.CUR: dwMoveMethod=SeekOrigin.Current; break;
				case SEEK.END: dwMoveMethod=SeekOrigin.End; break;
				default: dwMoveMethod=SeekOrigin.Begin; break;
			}

			return (uint)fd.Seek(off, dwMoveMethod);
		}

		static int tiffCloseProc(Stream fd)
		{
			fd.Close();
			return 0;
		}

		static uint tiffSizeProc(Stream fd)
		{
			return (uint)Math.Min(fd.Length, 0xffffffff);
		}

		// Open a TIFF file descriptor for read/writing.
		public static TIFF TIFFFdOpen(Stream ifd, string name, string mode)
		{
			TIFF tif=TIFFClientOpen(name, mode, ifd, tiffReadProc, tiffWriteProc, tiffSeekProc, tiffCloseProc, tiffSizeProc);
			if(tif!=null) tif.tif_fd=ifd;
			return tif;
		}

		// Open a TIFF file for read/writing.
		public static TIFF TIFFOpen(string name, string mode)
		{
			string module="TIFFOpen";
			Stream fd;

			O m=TIFFgetMode(mode, module);

			try
			{
				switch(m)
				{
					case O.RDONLY:
						fd=new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
						break;
					case O.RDWR:
						fd=new FileStream(name, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
						break;
					case O.RDWR|O.CREAT:
						fd=new FileStream(name, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
						break;
					case O.RDWR|O.TRUNC:
					case O.RDWR|O.CREAT|O.TRUNC:
						fd=new FileStream(name, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
						break;
					default: return null;
				}
			}
			catch
			{
				TIFFErrorExt(null, module, "{0}: Cannot open", name);
				return null;
			}

			TIFF tif=TIFFFdOpen(fd, name, mode);
			if(tif==null) fd.Close();
			return tif;
		}

		public static bool TIFFmemcmp(ushort[] p1, ushort[] p2, int c)
		{
			for(int i=0; i<c; i++) if(p1[i]!=p2[i]) return false;
			return true;
		}

		static void Win32WarningHandler(string module, string fmt, params object[] ap)
		{
			if(module!=null) Console.Error.Write(module+" ");
			Console.Error.WriteLine("Warning, "+fmt+".", ap);
		}

		static TIFFErrorHandler TIFFwarningHandler=Win32WarningHandler;

		static void Win32ErrorHandler(string module, string fmt, params object[] ap)
		{
			if(module!=null) Console.Error.Write(module+" ");
			Console.Error.WriteLine(fmt+".", ap);
		}

		static TIFFErrorHandler TIFFerrorHandler=Win32ErrorHandler;

		public static uint __GetAsUint(object[] ap, int index)
		{
			if(ap[index] is byte) return (uint)(byte)ap[index];
			if(ap[index] is sbyte) return (uint)(sbyte)ap[index];
			if(ap[index] is ushort) return (uint)(ushort)ap[index];
			if(ap[index] is short) return (uint)(short)ap[index];
			if(ap[index] is int) return (uint)(int)ap[index];
			if(ap[index] is uint) return (uint)ap[index];
			if(ap[index] is long) return (uint)(long)ap[index];
			if(ap[index] is ulong) return (uint)(ulong)ap[index];
			if(ap[index] is Enum) return (uint)(int)ap[index];
			throw new Exception("Unknown type");
		}

		public static int __GetAsInt(object[] ap, int index)
		{
			if(ap[index] is byte) return (int)(byte)ap[index];
			if(ap[index] is sbyte) return (int)(sbyte)ap[index];
			if(ap[index] is ushort) return (int)(ushort)ap[index];
			if(ap[index] is short) return (int)(short)ap[index];
			if(ap[index] is int) return (int)ap[index];
			if(ap[index] is uint) return (int)(uint)ap[index];
			if(ap[index] is long) return (int)(long)ap[index];
			if(ap[index] is ulong) return (int)(ulong)ap[index];
			if(ap[index] is Enum) return (int)ap[index];
			throw new Exception("Unknown type");
		}

		public static ushort __GetAsUshort(object[] ap, int index)
		{
			if(ap[index] is byte) return (ushort)(byte)ap[index];
			if(ap[index] is sbyte) return (ushort)(sbyte)ap[index];
			if(ap[index] is ushort) return (ushort)ap[index];
			if(ap[index] is short) return (ushort)(short)ap[index];
			if(ap[index] is int) return (ushort)(int)ap[index];
			if(ap[index] is uint) return (ushort)(uint)ap[index];
			if(ap[index] is long) return (ushort)(long)ap[index];
			if(ap[index] is ulong) return (ushort)(ulong)ap[index];
			if(ap[index] is Enum) return (ushort)(int)ap[index];
			throw new Exception("Unknown type");
		}

		public static short __GetAsShort(object[] ap, int index)
		{
			if(ap[index] is byte) return (short)(byte)ap[index];
			if(ap[index] is sbyte) return (short)(sbyte)ap[index];
			if(ap[index] is short) return (short)ap[index];
			if(ap[index] is ushort) return (short)(ushort)ap[index];
			if(ap[index] is int) return (short)(int)ap[index];
			if(ap[index] is uint) return (short)(uint)ap[index];
			if(ap[index] is long) return (short)(long)ap[index];
			if(ap[index] is ulong) return (short)(ulong)ap[index];
			if(ap[index] is Enum) return (short)(int)ap[index];
			throw new Exception("Unknown type");
		}

		public static byte __GetAsByte(object[] ap, int index)
		{
			if(ap[index] is byte) return (byte)ap[index];
			if(ap[index] is sbyte) return (byte)(sbyte)ap[index];
			if(ap[index] is ushort) return (byte)(ushort)ap[index];
			if(ap[index] is short) return (byte)(short)ap[index];
			if(ap[index] is int) return (byte)(int)ap[index];
			if(ap[index] is uint) return (byte)(uint)ap[index];
			if(ap[index] is long) return (byte)(long)ap[index];
			if(ap[index] is ulong) return (byte)(ulong)ap[index];
			if(ap[index] is Enum) return (byte)(int)ap[index];
			throw new Exception("Unknown type");
		}

		public static sbyte __GetAsSbyte(object[] ap, int index)
		{
			if(ap[index] is byte) return (sbyte)(byte)ap[index];
			if(ap[index] is sbyte) return (sbyte)ap[index];
			if(ap[index] is short) return (sbyte)(short)ap[index];
			if(ap[index] is ushort) return (sbyte)(ushort)ap[index];
			if(ap[index] is int) return (sbyte)(int)ap[index];
			if(ap[index] is uint) return (sbyte)(uint)ap[index];
			if(ap[index] is long) return (sbyte)(long)ap[index];
			if(ap[index] is ulong) return (sbyte)(ulong)ap[index];
			if(ap[index] is Enum) return (sbyte)(int)ap[index];
			throw new Exception("Unknown type");
		}

		public static float __GetAsFloat(object[] ap, int index)
		{
			if(ap[index] is float) return (float)ap[index];
			if(ap[index] is double) return (float)(double)ap[index];
			if(ap[index] is byte) return (float)(byte)ap[index];
			if(ap[index] is sbyte) return (float)(sbyte)ap[index];
			if(ap[index] is short) return (float)(short)ap[index];
			if(ap[index] is ushort) return (float)(ushort)ap[index];
			if(ap[index] is int) return (float)(int)ap[index];
			if(ap[index] is uint) return (float)(uint)ap[index];
			if(ap[index] is long) return (float)(long)ap[index];
			if(ap[index] is ulong) return (float)(ulong)ap[index];
			if(ap[index] is Enum) return (float)(int)ap[index];
			throw new Exception("Unknown type");
		}

		public static double __GetAsDouble(object[] ap, int index)
		{
			if(ap[index] is double) return (double)ap[index];
			if(ap[index] is float) return (double)(float)ap[index];
			if(ap[index] is byte) return (double)(byte)ap[index];
			if(ap[index] is sbyte) return (double)(sbyte)ap[index];
			if(ap[index] is short) return (double)(short)ap[index];
			if(ap[index] is ushort) return (double)(ushort)ap[index];
			if(ap[index] is int) return (double)(int)ap[index];
			if(ap[index] is uint) return (double)(uint)ap[index];
			if(ap[index] is long) return (double)(long)ap[index];
			if(ap[index] is ulong) return (double)(ulong)ap[index];
			if(ap[index] is Enum) return (double)(int)ap[index];
			throw new Exception("Unknown type");
		}
	}
}
