// tif_warning.cs
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
		static TIFFErrorHandlerExt TIFFwarningHandlerExt=null;

		public static TIFFErrorHandler TIFFSetWarningHandler(TIFFErrorHandler handler)
		{
			TIFFErrorHandler prev=TIFFwarningHandler;
			TIFFwarningHandler=handler;
			return prev;
		}

		public static TIFFErrorHandlerExt TIFFSetWarningHandlerExt(TIFFErrorHandlerExt handler)
		{
			TIFFErrorHandlerExt prev=TIFFwarningHandlerExt;
			TIFFwarningHandlerExt=handler;
			return prev;
		}

		public static void TIFFWarning(string module, string fmt, params object[] ap)
		{
			if(TIFFwarningHandler!=null) TIFFwarningHandler(module, fmt, ap);
			if(TIFFwarningHandlerExt!=null) TIFFwarningHandlerExt(null, module, fmt, ap);
		}

		public static void TIFFWarningExt(Stream fd, string module, string fmt, params object[] ap)
		{
			if(TIFFwarningHandler!=null) TIFFwarningHandler(module, fmt, ap);
			if(TIFFwarningHandlerExt!=null) TIFFwarningHandlerExt(fd, module, fmt, ap);
		}
	}
}
