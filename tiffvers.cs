// tiffvers.cs
//
// Based on LIBTIFF, Version 3.9.4 - 15-Jun-2010
// Copyright (c) 2006-2010 by the Authors

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		public const string TIFFLIB_VERSION_STR="C# port of 'LIBTIFF, Version 3.9.4'\nCopyright (c) 2006-2010 by the Authors\nCopyright (c) 1988-1996 Sam Leffler\nCopyright (c) 1991-1996 Silicon Graphics, Inc.";

		// This define can be used in code that requires
		// compilation-related definitions specific to a
		// version or versions of the library. Runtime
		// version checking should be done based on the
		// string returned by TIFFGetVersion.
		public const int TIFFLIB_VERSION=20100615;
	}
}